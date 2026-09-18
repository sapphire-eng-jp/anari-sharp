// Copyright 2026 Sapphire ENG. Co., Ltd.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Drawing.Imaging;
using System.Numerics;
using ANARISharp;
using ANARISharp.Interop;

namespace SphereViewerWinForms;

/// <summary>
/// Interactive ANARI viewer: renders randomly placed spheres and lets the user
/// orbit the camera with the mouse (left-drag to rotate, wheel to zoom).
/// The toolbar sets the number of spheres and regenerates the scene.
/// </summary>
public sealed class MainForm : Form
{
    private const int DefaultSphereCount = 200;
    private const int MaxSphereCount = 1_000_000;
    private const int MaxAccumulationFrames = 64;

    private readonly ANARILibrary _library;
    private readonly ANARIDevice _device;
    private ANARIWorld _world;
    private readonly ANARICamera _camera;
    private readonly ANARIRenderer _renderer;
    private readonly ANARIFrame _frame;

    private readonly ViewportPanel _viewport;
    private readonly TextBox _countTextBox;
    private readonly Button _applyButton;

    private readonly System.Windows.Forms.Timer _timer;
    private Bitmap? _bitmap;

    // Render loop state: at most one frame in flight; re-render when dirty.
    // While the camera is still, keep rendering to accumulate samples
    // (KHR_FRAME_ACCUMULATION) until MaxAccumulationFrames is reached.
    private bool _frameInFlight;
    private bool _dirty = true;
    private int _accumulatedFrames;

    // Orbit camera state (angles in radians, target fixed at the origin).
    private float _azimuth = 0.5f;
    private float _elevation = 0.3f;
    private float _distance = 4.0f;
    private Point _lastMouse;
    private bool _dragging;

    public MainForm(string libraryName)
    {
        Text = $"ANARI Sphere Viewer (WinForms, {libraryName})";
        ClientSize = new Size(800, 632);

        // Toolbar: sphere count input and regenerate button.
        var countLabel = new Label
        {
            Text = "Spheres:",
            AutoSize = true,
            Margin = new Padding(6, 8, 3, 0),
        };
        _countTextBox = new TextBox
        {
            Width = 80,
            Text = DefaultSphereCount.ToString(),
            Margin = new Padding(3, 5, 3, 3),
        };
        _applyButton = new Button
        {
            Text = "Regenerate",
            AutoSize = true,
            Margin = new Padding(3),
        };
        _applyButton.Click += OnApplyClicked;
        AcceptButton = _applyButton;

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
        };
        toolbar.Controls.AddRange(new Control[] { countLabel, _countTextBox, _applyButton });

        _viewport = new ViewportPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(20, 20, 31),
        };
        _viewport.Paint += OnViewportPaint;
        _viewport.Resize += (_, _) => _dirty = true;
        _viewport.MouseDown += OnViewportMouseDown;
        _viewport.MouseMove += OnViewportMouseMove;
        _viewport.MouseUp += OnViewportMouseUp;
        _viewport.MouseWheel += OnViewportMouseWheel;

        Controls.Add(_viewport);
        Controls.Add(toolbar);
        _viewport.BringToFront(); // ensure the fill panel lays out after the docked toolbar

        _library = ANARILibrary.Load(libraryName,
            (device, source, sourceType, severity, code, message) =>
                Debug.WriteLine($"[{severity}] {message}"));

        _device = _library.NewDevice();
        _device.CommitParameters();

        _world = SphereScene.CreateWorld(_device, DefaultSphereCount);

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

        _timer = new System.Windows.Forms.Timer { Interval = 15 };
        _timer.Tick += (_, _) => PumpRenderLoop();
        _timer.Start();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _viewport.Focus(); // so the mouse wheel zooms without clicking first
    }

    /// <summary>Rebuilds the world with the sphere count entered in the toolbar.</summary>
    private void OnApplyClicked(object? sender, EventArgs e)
    {
        if (!int.TryParse(_countTextBox.Text, out int count) || count < 1 || count > MaxSphereCount)
        {
            MessageBox.Show(this, $"Enter a sphere count between 1 and {MaxSphereCount}.",
                "Invalid sphere count", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var newWorld = SphereScene.CreateWorld(_device, count);
        _frame.SetParameter("world", newWorld); // takes effect on the next frame commit
        _world.Dispose();
        _world = newWorld;
        _dirty = true;
        _viewport.Focus();
    }

    /// <summary>Starts a render when needed and presents the result when ready.</summary>
    private void PumpRenderLoop()
    {
        if (_frameInFlight)
        {
            if (!_frame.IsReady)
                return;
            _frameInFlight = false;
            _accumulatedFrames++;
            PresentFrame();
        }

        var size = _viewport.ClientSize;
        if (size.Width <= 0 || size.Height <= 0)
            return;

        if (_dirty)
        {
            _dirty = false;
            UpdateCamera(size.Width, size.Height);
            _frame.SetParameter("size", new UVec2((uint)size.Width, (uint)size.Height));
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

    /// <summary>Copies the rendered frame into the backing bitmap (flip + RGBA to BGRA).</summary>
    private unsafe void PresentFrame()
    {
        var mapped = _frame.Map<byte>("channel.color");
        int w = (int)mapped.Width;
        int h = (int)mapped.Height;
        if (w == 0 || h == 0)
        {
            _frame.Unmap("channel.color");
            return;
        }

        if (_bitmap == null || _bitmap.Width != w || _bitmap.Height != h)
        {
            _bitmap?.Dispose();
            _bitmap = new Bitmap(w, h, PixelFormat.Format32bppRgb);
        }

        var bits = _bitmap.LockBits(new Rectangle(0, 0, w, h),
            ImageLockMode.WriteOnly, PixelFormat.Format32bppRgb);
        try
        {
            // The frame buffer is bottom-up RGBA; GDI+ bitmaps are top-down BGRA.
            var src = mapped.Data;
            for (int y = 0; y < h; y++)
            {
                var srcRow = src.Slice((h - 1 - y) * w * 4, w * 4);
                byte* dst = (byte*)bits.Scan0 + (long)y * bits.Stride;
                for (int x = 0; x < w; x++)
                {
                    dst[x * 4 + 0] = srcRow[x * 4 + 2];
                    dst[x * 4 + 1] = srcRow[x * 4 + 1];
                    dst[x * 4 + 2] = srcRow[x * 4 + 0];
                    dst[x * 4 + 3] = 255;
                }
            }
        }
        finally
        {
            _bitmap.UnlockBits(bits);
            _frame.Unmap("channel.color");
        }

        _viewport.Invalidate();
    }

    private void OnViewportPaint(object? sender, PaintEventArgs e)
    {
        if (_bitmap != null)
            e.Graphics.DrawImage(_bitmap, _viewport.ClientRectangle);
    }

    private void OnViewportMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _dragging = true;
            _lastMouse = e.Location;
            _viewport.Focus();
            _viewport.Capture = true;
        }
    }

    private void OnViewportMouseMove(object? sender, MouseEventArgs e)
    {
        if (!_dragging)
            return;

        const float sensitivity = 0.01f;
        _azimuth -= (e.X - _lastMouse.X) * sensitivity;
        _elevation += (e.Y - _lastMouse.Y) * sensitivity;
        _elevation = Math.Clamp(_elevation, -1.55f, 1.55f);
        _lastMouse = e.Location;
        _dirty = true;
    }

    private void OnViewportMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _dragging = false;
            _viewport.Capture = false;
        }
    }

    private void OnViewportMouseWheel(object? sender, MouseEventArgs e)
    {
        _distance = Math.Clamp(_distance * MathF.Exp(-e.Delta * 0.001f), 1.0f, 20.0f);
        _dirty = true;
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);
        _timer.Dispose();
        if (_frameInFlight)
            _frame.Wait();
        _frame.Dispose();
        _renderer.Dispose();
        _camera.Dispose();
        _world.Dispose();
        _device.Dispose();
        _library.Dispose();
        _bitmap?.Dispose();
    }

    /// <summary>Double-buffered, focusable panel used as the render viewport.</summary>
    private sealed class ViewportPanel : Panel
    {
        public ViewportPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
        }
    }
}
