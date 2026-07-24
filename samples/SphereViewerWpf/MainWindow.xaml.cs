// Copyright 2026 Sapphire ENG. Co., Ltd.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Numerics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ANARISharp;
using ANARISharp.Interop;

namespace SphereViewerWpf;

/// <summary>
/// Interactive ANARI viewer: renders randomly placed spheres and lets the user
/// orbit the camera with the mouse (left-drag to rotate, wheel to zoom).
/// The toolbar sets the number of spheres and regenerates the scene.
/// </summary>
public partial class MainWindow : Window
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

    private readonly DispatcherTimer _timer;
    private WriteableBitmap? _bitmap;
    private byte[] _pixels = System.Array.Empty<byte>();

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

    public MainWindow(string libraryName)
    {
        InitializeComponent();
        Title = $"ANARI Sphere Viewer (WPF, {libraryName})";

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

        SizeChanged += (_, _) => _dirty = true;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(15) };
        _timer.Tick += (_, _) => PumpRenderLoop();
        _timer.Start();
    }

    /// <summary>Rebuilds the world with the sphere count entered in the toolbar.</summary>
    private void OnApplyClicked(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(CountTextBox.Text, out int count) || count < 1 || count > MaxSphereCount)
        {
            MessageBox.Show(this, $"Enter a sphere count between 1 and {MaxSphereCount}.",
                "Invalid sphere count", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var newWorld = SphereScene.CreateWorld(_device, count);
        _frame.SetParameter("world", newWorld); // takes effect on the next frame commit
        _world.Dispose();
        _world = newWorld;
        _dirty = true;
    }

    /// <summary>Target render size in device pixels (viewport size times DPI scale).</summary>
    private (int Width, int Height) GetRenderSize()
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        return ((int)(Viewport.ActualWidth * dpi.DpiScaleX), (int)(Viewport.ActualHeight * dpi.DpiScaleY));
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

        var (w, h) = GetRenderSize();
        if (w <= 0 || h <= 0)
            return;

        if (_dirty)
        {
            _dirty = false;
            UpdateCamera(w, h);
            _frame.SetParameter("size", new UVec2((uint)w, (uint)h));
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

    /// <summary>Copies the rendered frame into the writeable bitmap (flip + RGBA to BGRA).</summary>
    private void PresentFrame()
    {
        var mapped = _frame.Map<byte>("channel.color");
        int w = (int)mapped.Width;
        int h = (int)mapped.Height;
        if (w == 0 || h == 0)
        {
            _frame.Unmap("channel.color");
            return;
        }

        if (_bitmap == null || _bitmap.PixelWidth != w || _bitmap.PixelHeight != h)
        {
            _bitmap = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgr32, null);
            ViewportImage.Source = _bitmap;
        }
        if (_pixels.Length != w * h * 4)
            _pixels = new byte[w * h * 4];

        // The frame buffer is bottom-up RGBA; the bitmap is top-down BGRA.
        var src = mapped.Data;
        for (int y = 0; y < h; y++)
        {
            var srcRow = src.Slice((h - 1 - y) * w * 4, w * 4);
            int dstRow = y * w * 4;
            for (int x = 0; x < w; x++)
            {
                _pixels[dstRow + x * 4 + 0] = srcRow[x * 4 + 2];
                _pixels[dstRow + x * 4 + 1] = srcRow[x * 4 + 1];
                _pixels[dstRow + x * 4 + 2] = srcRow[x * 4 + 0];
                _pixels[dstRow + x * 4 + 3] = 255;
            }
        }
        _frame.Unmap("channel.color");

        _bitmap.WritePixels(new Int32Rect(0, 0, w, h), _pixels, w * 4, 0);
    }

    private void OnViewportMouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragging = true;
        _lastMouse = e.GetPosition(Viewport);
        Viewport.CaptureMouse();
    }

    private void OnViewportMouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragging)
            return;

        const float sensitivity = 0.01f;
        var pos = e.GetPosition(Viewport);
        _azimuth -= (float)(pos.X - _lastMouse.X) * sensitivity;
        _elevation += (float)(pos.Y - _lastMouse.Y) * sensitivity;
        _elevation = Math.Clamp(_elevation, -1.55f, 1.55f);
        _lastMouse = pos;
        _dirty = true;
    }

    private void OnViewportMouseUp(object sender, MouseButtonEventArgs e)
    {
        _dragging = false;
        Viewport.ReleaseMouseCapture();
    }

    private void OnViewportMouseWheel(object sender, MouseWheelEventArgs e)
    {
        _distance = Math.Clamp(_distance * MathF.Exp(-e.Delta * 0.001f), 1.0f, 20.0f);
        _dirty = true;
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _timer.Stop();
        if (_frameInFlight)
            _frame.Wait();
        _frame.Dispose();
        _renderer.Dispose();
        _camera.Dispose();
        _world.Dispose();
        _device.Dispose();
        _library.Dispose();
    }
}
