// Copyright 2026 Sapphire ENG. Co., Ltd.
// SPDX-License-Identifier: Apache-2.0

using System.Numerics;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using ANARISharp;
using ANARISharp.Interop;

namespace SphereViewerSilk;

/// <summary>
/// Interactive ANARI viewer on Silk.NET: renders randomly placed spheres and
/// lets the user orbit the camera with the mouse (left-drag to rotate, wheel to
/// zoom). Up/Down doubles/halves the sphere count; Escape quits.
/// The rendered frame is uploaded to an OpenGL texture and drawn as a
/// fullscreen triangle.
/// </summary>
internal sealed class Viewer : IDisposable
{
    private const int DefaultSphereCount = 200;
    private const int MaxSphereCount = 1_000_000;
    private const int MaxAccumulationFrames = 64;

    private readonly string _libraryName;
    private readonly IWindow _window;
    private GL? _gl;
    private IInputContext? _input;

    private ANARILibrary _library = null!;
    private ANARIDevice _device = null!;
    private ANARIWorld _world = null!;
    private ANARICamera _camera = null!;
    private ANARIRenderer _renderer = null!;
    private ANARIFrame _frame = null!;

    // OpenGL presentation resources.
    private uint _texture;
    private uint _program;
    private uint _vao;
    private int _texWidth;
    private int _texHeight;

    // Render loop state: at most one frame in flight; re-render when dirty.
    // While the camera is still, keep rendering to accumulate samples
    // (KHR_FRAME_ACCUMULATION) until MaxAccumulationFrames is reached.
    private bool _frameInFlight;
    private bool _dirty = true;
    private int _accumulatedFrames;
    private int _sphereCount = DefaultSphereCount;

    // Orbit camera state (angles in radians, target fixed at the origin).
    private float _azimuth = 0.5f;
    private float _elevation = 0.3f;
    private float _distance = 4.0f;
    private Vector2 _lastPointer;
    private bool _dragging;

    public Viewer(string libraryName)
    {
        _libraryName = libraryName;

        var options = WindowOptions.Default with
        {
            Size = new Vector2D<int>(800, 640),
            Title = BuildTitle(),
            VSync = true,
        };
        _window = Window.Create(options);
        _window.Load += OnLoad;
        _window.Render += OnRender;
        _window.FramebufferResize += OnFramebufferResize;
        _window.Closing += OnClosing;
    }

    public void Run() => _window.Run();

    private string BuildTitle() =>
        $"ANARI Sphere Viewer (Silk.NET, {_libraryName}) - {_sphereCount} spheres [Up/Down: count, Esc: quit]";

    private void OnLoad()
    {
        _gl = _window.CreateOpenGL();
        _input = _window.CreateInput();

        var mouse = _input.Mice[0];
        mouse.MouseDown += (m, button) =>
        {
            if (button == MouseButton.Left)
            {
                _dragging = true;
                _lastPointer = m.Position;
            }
        };
        mouse.MouseUp += (_, button) =>
        {
            if (button == MouseButton.Left)
                _dragging = false;
        };
        mouse.MouseMove += OnMouseMove;
        mouse.Scroll += (_, wheel) =>
        {
            _distance = Math.Clamp(_distance * MathF.Exp(-wheel.Y * 0.12f), 1.0f, 20.0f);
            _dirty = true;
        };
        _input.Keyboards[0].KeyDown += OnKeyDown;

        _library = ANARILibrary.Load(_libraryName,
            (device, source, sourceType, severity, code, message) =>
                Console.Error.WriteLine($"[{severity}] {message}"));

        _device = _library.NewDevice();
        _device.CommitParameters();

        _world = SphereScene.CreateWorld(_device, _sphereCount);

        _camera = _device.NewCamera("perspective");

        _renderer = _device.NewRenderer("default");
        _renderer.SetParameter("background", new Vector4(0.08f, 0.08f, 0.12f, 1.0f));
        _renderer.CommitParameters();

        _frame = _device.NewFrame();
        _frame.SetParameter("channel.color", ANARIDataType.ANARI_UFIXED8_RGBA_SRGB);
        _frame.SetParameter("accumulation", true);
        _frame.SetParameter("world", _world);
        _frame.SetParameter("camera", _camera);
        _frame.SetParameter("renderer", _renderer);

        CreatePresentationResources(_gl);
    }

    /// <summary>Compiles the blit shader and creates the texture the frame is uploaded to.</summary>
    private void CreatePresentationResources(GL gl)
    {
        const string vertexSource = """
            #version 330 core
            out vec2 uv;
            void main() {
                // Fullscreen triangle generated from gl_VertexID; no vertex buffer needed.
                vec2 p = vec2((gl_VertexID << 1) & 2, gl_VertexID & 2);
                uv = p;
                gl_Position = vec4(p * 2.0 - 1.0, 0.0, 1.0);
            }
            """;
        const string fragmentSource = """
            #version 330 core
            uniform sampler2D frameTex;
            in vec2 uv;
            out vec4 color;
            void main() { color = texture(frameTex, uv); }
            """;

        uint vs = CompileShader(gl, ShaderType.VertexShader, vertexSource);
        uint fs = CompileShader(gl, ShaderType.FragmentShader, fragmentSource);
        _program = gl.CreateProgram();
        gl.AttachShader(_program, vs);
        gl.AttachShader(_program, fs);
        gl.LinkProgram(_program);
        gl.GetProgram(_program, ProgramPropertyARB.LinkStatus, out int linked);
        if (linked == 0)
            throw new InvalidOperationException(gl.GetProgramInfoLog(_program));
        gl.DeleteShader(vs);
        gl.DeleteShader(fs);

        // A core profile needs a bound VAO even when no vertex attributes are used.
        _vao = gl.GenVertexArray();

        _texture = gl.GenTexture();
        gl.BindTexture(TextureTarget.Texture2D, _texture);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
    }

    private static uint CompileShader(GL gl, ShaderType type, string source)
    {
        uint shader = gl.CreateShader(type);
        gl.ShaderSource(shader, source);
        gl.CompileShader(shader);
        gl.GetShader(shader, ShaderParameterName.CompileStatus, out int ok);
        if (ok == 0)
            throw new InvalidOperationException(gl.GetShaderInfoLog(shader));
        return shader;
    }

    /// <summary>Called every vsync tick: pumps the ANARI render loop and blits the latest frame.</summary>
    private void OnRender(double deltaSeconds)
    {
        var gl = _gl!;

        if (_frameInFlight && _frame.IsReady)
        {
            _frameInFlight = false;
            _accumulatedFrames++;
            UploadFrame(gl);
        }

        var size = _window.FramebufferSize;
        if (!_frameInFlight && size.X > 0 && size.Y > 0)
        {
            if (_dirty)
            {
                _dirty = false;
                UpdateCamera(size.X, size.Y);
                _frame.SetParameter("size", new UVec2((uint)size.X, (uint)size.Y));
                _frame.CommitParameters(); // committing the frame also resets accumulation
                _accumulatedFrames = 0;
                _frame.Render();
                _frameInFlight = true;
            }
            else if (_accumulatedFrames < MaxAccumulationFrames)
            {
                // Scene unchanged: render again to accumulate one more sample pass.
                _frame.Render();
                _frameInFlight = true;
            }
        }

        // Always redraw the most recent texture so the window stays valid while a frame is in flight.
        gl.Clear(ClearBufferMask.ColorBufferBit);
        if (_texWidth > 0)
        {
            gl.UseProgram(_program);
            gl.BindVertexArray(_vao);
            gl.BindTexture(TextureTarget.Texture2D, _texture);
            gl.DrawArrays(PrimitiveType.Triangles, 0, 3);
        }
    }

    /// <summary>
    /// Uploads the rendered frame into the GL texture. ANARI frames are
    /// bottom-up, matching OpenGL's texture convention, so no row flip is needed.
    /// The channel is already sRGB-encoded, so it is stored as plain RGBA8
    /// (an SRGB8_ALPHA8 texture would apply the transfer function twice).
    /// </summary>
    private unsafe void UploadFrame(GL gl)
    {
        var mapped = _frame.Map<byte>("channel.color");
        int w = (int)mapped.Width;
        int h = (int)mapped.Height;
        if (w > 0 && h > 0)
        {
            gl.BindTexture(TextureTarget.Texture2D, _texture);
            fixed (byte* pixels = mapped.Data)
            {
                if (w != _texWidth || h != _texHeight)
                {
                    gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8,
                        (uint)w, (uint)h, 0, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);
                    (_texWidth, _texHeight) = (w, h);
                }
                else
                {
                    gl.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0,
                        (uint)w, (uint)h, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);
                }
            }
        }
        _frame.Unmap("channel.color");
    }

    /// <summary>Recomputes the orbit camera and commits its parameters.</summary>
    private void UpdateCamera(int width, int height)
    {
        float ce = MathF.Cos(_elevation);
        var position = _distance * new Vector3(
            ce * MathF.Sin(_azimuth),
            MathF.Sin(_elevation),
            ce * MathF.Cos(_azimuth));

        _camera.SetParameter("position", position);
        _camera.SetParameter("direction", Vector3.Normalize(-position));
        _camera.SetParameter("up", new Vector3(0f, 1f, 0f));
        _camera.SetParameter("aspect", (float)width / height);
        _camera.CommitParameters();
    }

    private void OnMouseMove(IMouse mouse, Vector2 position)
    {
        if (!_dragging)
            return;

        const float sensitivity = 0.01f;
        _azimuth -= (position.X - _lastPointer.X) * sensitivity;
        _elevation += (position.Y - _lastPointer.Y) * sensitivity;
        _elevation = Math.Clamp(_elevation, -1.55f, 1.55f);
        _lastPointer = position;
        _dirty = true;
    }

    private void OnKeyDown(IKeyboard keyboard, Key key, int scancode)
    {
        switch (key)
        {
            case Key.Up:
                SetSphereCount(Math.Min(_sphereCount * 2, MaxSphereCount));
                break;
            case Key.Down:
                SetSphereCount(Math.Max(_sphereCount / 2, 1));
                break;
            case Key.Escape:
                _window.Close();
                break;
        }
    }

    /// <summary>Rebuilds the world with a new sphere count.</summary>
    private void SetSphereCount(int count)
    {
        if (count == _sphereCount)
            return;
        _sphereCount = count;

        var newWorld = SphereScene.CreateWorld(_device, count);
        _frame.SetParameter("world", newWorld); // takes effect on the next frame commit
        _world.Dispose();
        _world = newWorld;
        _dirty = true;
        _window.Title = BuildTitle();
    }

    private void OnFramebufferResize(Vector2D<int> size)
    {
        _gl!.Viewport(size);
        _dirty = true;
    }

    /// <summary>Releases ANARI and GL objects while the GL context is still current.</summary>
    private void OnClosing()
    {
        if (_frameInFlight)
            _frame.Wait();
        _frame.Dispose();
        _renderer.Dispose();
        _camera.Dispose();
        _world.Dispose();
        _device.Dispose();
        _library.Dispose();

        if (_gl != null)
        {
            _gl.DeleteTexture(_texture);
            _gl.DeleteVertexArray(_vao);
            _gl.DeleteProgram(_program);
        }
    }

    public void Dispose()
    {
        _input?.Dispose();
        _gl?.Dispose();
        _window.Dispose();
    }
}
