using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Windows.UI;
using WinRT.Interop;

namespace ScreenProtector;

public sealed partial class OverlayWindow : Window
{
    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

    private const int GWL_STYLE = -16;
    private const int GWL_EXSTYLE = -20;
    private const int WS_POPUP = unchecked((int)0x80000000);
    private const int WS_VISIBLE = 0x10000000;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOPMOST = 0x00000008;
    private const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_FRAMECHANGED = 0x0020;
    private const uint SWP_NOACTIVATE = 0x0010;
    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

    private IntPtr _hwnd;
    private bool _windowInitialized;
    private float _scanlineY;
    private byte[]? _pixelBuffer;
    private CanvasBitmap? _screenBitmap;
    private int _captureWidth;
    private int _captureHeight;
    private bool _hasCapturedFrame;
    private TimeSpan _captureAccumulator = TimeSpan.Zero;
    private Color _pixelTintColor = Color.FromArgb(255, 155, 188, 15);

    public OverlayWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        Activated += OverlayWindow_Activated;
    }

    public enum EffectType { CRT, Pixelate }

    public EffectType CurrentEffect { get; set; } = EffectType.CRT;
    public float CrtSpeed { get; set; } = 18f;
    public float CrtOpacity { get; set; } = 0.3f;
    public int CrtColorFilterIndex { get; set; }
    public float CrtScanlineWidth { get; set; } = 2f;
    public float PixelSize { get; set; } = 16f;
    public bool PixelMonochrome { get; set; }
    public string PixelMonochromeColorHex { get; set; } = "#9BBC0F";
    public int CaptureFrameRate { get; set; } = 30;
    public int OutputFrameRate { get; set; } = 60;
    public bool IsClickThrough { get; set; } = true;

    private void OverlayWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        EnsureWindowConfigured();
    }

    public void RefreshWindowBehavior()
    {
        EnsureWindowConfigured();
    }

    private void EnsureWindowConfigured()
    {
        _hwnd = WindowNative.GetWindowHandle(this);
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        if (!_windowInitialized)
        {
            AppWindow.SetPresenter(Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen);
            _windowInitialized = true;
        }

        ApplyWindowStyles();
        ApplyHitTesting();
        SetWindowDisplayAffinity(_hwnd, WDA_EXCLUDEFROMCAPTURE);
        SetWindowPos(_hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_FRAMECHANGED);
    }

    private void ApplyWindowStyles()
    {
        int style = GetWindowLong(_hwnd, GWL_STYLE);
        style |= WS_POPUP | WS_VISIBLE;
        SetWindowLong(_hwnd, GWL_STYLE, style);

        int exStyle = GetWindowLong(_hwnd, GWL_EXSTYLE);
        exStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST;
        exStyle = IsClickThrough
            ? exStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED
            : exStyle & ~(WS_EX_TRANSPARENT | WS_EX_LAYERED);
        SetWindowLong(_hwnd, GWL_EXSTYLE, exStyle);
    }

    private void ApplyHitTesting()
    {
        bool isHitTestVisible = !IsClickThrough;
        RootGrid.IsHitTestVisible = isHitTestVisible;
        CanvasControl.IsHitTestVisible = isHitTestVisible;
    }

    private void CanvasControl_Update(ICanvasAnimatedControl sender, CanvasAnimatedUpdateEventArgs args)
    {
        sender.TargetElapsedTime = TimeSpan.FromSeconds(1d / Math.Clamp(OutputFrameRate, 5, 60));

        if (CurrentEffect == EffectType.CRT)
        {
            _scanlineY += CrtSpeed * (float)args.Timing.ElapsedTime.TotalSeconds * 60f;
            if (_scanlineY > sender.Size.Height)
            {
                _scanlineY = 0;
            }
        }

        _captureAccumulator += args.Timing.ElapsedTime;
        TimeSpan captureInterval = TimeSpan.FromSeconds(1d / Math.Clamp(CaptureFrameRate, 5, 60));
        if (_captureAccumulator < captureInterval)
        {
            return;
        }

        _captureAccumulator = TimeSpan.Zero;
        _pixelTintColor = ParseHexColor(PixelMonochromeColorHex, Color.FromArgb(255, 155, 188, 15));

        int screenWidth = ScreenCapture.ScreenWidth;
        int screenHeight = ScreenCapture.ScreenHeight;
        if (screenWidth <= 0 || screenHeight <= 0)
        {
            return;
        }

        int destWidth = screenWidth;
        int destHeight = screenHeight;
        if (CurrentEffect == EffectType.Pixelate)
        {
            destWidth = Math.Max((int)(screenWidth / Math.Max(PixelSize, 1f)), 1);
            destHeight = Math.Max((int)(screenHeight / Math.Max(PixelSize, 1f)), 1);
        }

        if (_pixelBuffer == null || _captureWidth != destWidth || _captureHeight != destHeight)
        {
            _pixelBuffer = new byte[destWidth * destHeight * 4];
            _captureWidth = destWidth;
            _captureHeight = destHeight;
            _hasCapturedFrame = false;
        }

        if (_pixelBuffer != null)
        {
            _hasCapturedFrame = ScreenCapture.CaptureScreen(destWidth, destHeight, _pixelBuffer);
        }
    }

    private void CanvasControl_Draw(ICanvasAnimatedControl sender, CanvasAnimatedDrawEventArgs args)
    {
        float width = (float)sender.Size.Width;
        float height = (float)sender.Size.Height;
        var drawingSession = args.DrawingSession;

        DrawCapturedScreen(sender, drawingSession, width, height);

        if (CurrentEffect == EffectType.CRT)
        {
            DrawCrtOverlay(drawingSession, width, height);
        }
    }

    private void DrawCapturedScreen(ICanvasAnimatedControl sender, CanvasDrawingSession drawingSession, float width, float height)
    {
        if (!_hasCapturedFrame || _pixelBuffer == null || _captureWidth <= 0 || _captureHeight <= 0)
        {
            return;
        }

        if (_screenBitmap != null && (_screenBitmap.SizeInPixels.Width != _captureWidth || _screenBitmap.SizeInPixels.Height != _captureHeight))
        {
            _screenBitmap.Dispose();
            _screenBitmap = null;
        }

        if (_screenBitmap == null)
        {
            _screenBitmap = CanvasBitmap.CreateFromBytes(
                sender.Device,
                _pixelBuffer,
                _captureWidth,
                _captureHeight,
                Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized);
        }
        else
        {
            _screenBitmap.SetPixelBytes(_pixelBuffer);
        }

        if (CurrentEffect == EffectType.Pixelate)
        {
            ICanvasImage source = PixelMonochrome ? CreatePixelMonochromeEffect() : _screenBitmap!;
            drawingSession.DrawImage(
                source,
                new Windows.Foundation.Rect(0, 0, width, height),
                new Windows.Foundation.Rect(0, 0, _captureWidth, _captureHeight),
                1f,
                CanvasImageInterpolation.NearestNeighbor);
            return;
        }

        drawingSession.DrawImage(_screenBitmap, new Windows.Foundation.Rect(0, 0, width, height));
    }

    private void DrawCrtOverlay(CanvasDrawingSession drawingSession, float width, float height)
    {
        Color filterColor = CrtColorFilterIndex switch
        {
            0 => Color.FromArgb((byte)(CrtOpacity * 255), 255, 176, 0),
            1 => Color.FromArgb((byte)(CrtOpacity * 255), 0, 255, 0),
            2 => Color.FromArgb((byte)(CrtOpacity * 255), 0, 0, 0),
            _ => Color.FromArgb(0, 0, 0, 0)
        };

        drawingSession.FillRectangle(0, 0, width, height, filterColor);

        Color scanlineColor = Color.FromArgb((byte)(CrtOpacity * 100), 0, 0, 0);
        float spacing = Math.Max(CrtScanlineWidth * 2f, 2f);
        float thickness = Math.Max(CrtScanlineWidth, 1f);
        for (float y = _scanlineY % spacing; y < height; y += spacing)
        {
            drawingSession.DrawLine(0, y, width, y, scanlineColor, thickness);
        }
    }

    private ColorMatrixEffect CreatePixelMonochromeEffect()
    {
        float red = _pixelTintColor.R / 255f;
        float green = _pixelTintColor.G / 255f;
        float blue = _pixelTintColor.B / 255f;

        return new ColorMatrixEffect
        {
            Source = _screenBitmap,
            ColorMatrix = new Matrix5x4
            {
                M11 = 0.2126f * red,
                M12 = 0.2126f * green,
                M13 = 0.2126f * blue,
                M21 = 0.7152f * red,
                M22 = 0.7152f * green,
                M23 = 0.7152f * blue,
                M31 = 0.0722f * red,
                M32 = 0.0722f * green,
                M33 = 0.0722f * blue,
                M44 = 1f
            }
        };
    }

    private static Color ParseHexColor(string? hex, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return fallback;
        }

        string normalized = hex.TrimStart('#');
        if (normalized.Length != 6)
        {
            return fallback;
        }

        try
        {
            byte red = Convert.ToByte(normalized.Substring(0, 2), 16);
            byte green = Convert.ToByte(normalized.Substring(2, 2), 16);
            byte blue = Convert.ToByte(normalized.Substring(4, 2), 16);
            return Color.FromArgb(255, red, green, blue);
        }
        catch
        {
            return fallback;
        }
    }
}