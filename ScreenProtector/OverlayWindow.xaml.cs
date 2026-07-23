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
    private IScreenCaptureBackend _captureBackend;
    private readonly OverlayRenderSettings _renderSettings = new();

    public OverlayWindow()
    {
        InitializeComponent();
        _captureBackend = ScreenCapture.CreateBackend();
        ExtendsContentIntoTitleBar = true;
        Activated += OverlayWindow_Activated;
    }

    public enum EffectType { CRT, Pixelate, GameBoy, VHS, Dither }

    public EffectType CurrentEffect { get; set; } = EffectType.CRT;
    public float CrtSpeed { get; set; } = 18f;
    public float CrtOpacity { get; set; } = 0.3f;
    public int CrtColorFilterIndex { get; set; }
    public float CrtScanlineWidth { get; set; } = 2f;
    public float PixelSize { get; set; } = 16f;
    public bool PixelMonochrome { get; set; }
    public string PixelMonochromeColorHex { get; set; } = "#9BBC0F";
    public float PixelBrightness { get; set; } = 1f;
    public float GameBoyPixelSize { get; set; } = 4f;
    public bool GameBoyGhosting { get; set; } = true;
    public float GameBoyBrightness { get; set; } = 1f;
    public int DitherCellSize { get; set; } = 4;
    public float DitherDarkAlpha { get; set; } = 0.38f;
    public int CaptureFrameRate { get; set; } = 30;
    public int OutputFrameRate { get; set; } = 60;
    public bool IsClickThrough { get; set; } = true;
    public float VhsGlitchAmount { get; set; } = 0.35f;
    public float VhsNoiseAmount { get; set; } = 0.18f;
    public string VhsTintColorHex { get; set; } = "#FF5050";
    public float VhsTrackingStrength { get; set; } = 0.35f;
    public float CrtMotionStrength { get; set; } = 0.4f;
    public string PreferredGraphicsAdapterId { get; set; } = string.Empty;

    public string CaptureBackendName => _captureBackend.BackendName;

    private void OverlayWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        EnsureWindowConfigured();
    }

    public void RefreshWindowBehavior()
    {
        EnsureWindowConfigured();
    }

    public void RecreateCaptureBackend()
    {
        _captureBackend.Dispose();
        _captureBackend = ScreenCapture.CreateBackend(PreferredGraphicsAdapterId);
        _pixelBuffer = null;
        _captureWidth = 0;
        _captureHeight = 0;
        _hasCapturedFrame = false;
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
        SyncRenderSettings();
        sender.TargetElapsedTime = TimeSpan.FromSeconds(1d / Math.Clamp(_renderSettings.OutputFrameRate, 5, 60));

        if (_renderSettings.EffectType == EffectType.CRT || _renderSettings.EffectType == EffectType.GameBoy || _renderSettings.EffectType == EffectType.VHS || _renderSettings.EffectType == EffectType.Dither)
        {
            _scanlineY += (_renderSettings.CrtSpeed + (_renderSettings.CrtMotionStrength * 24f)) * (float)args.Timing.ElapsedTime.TotalSeconds * 60f;
            if (_scanlineY > sender.Size.Height)
            {
                _scanlineY = 0;
            }
        }

        _captureAccumulator += args.Timing.ElapsedTime;
        TimeSpan captureInterval = TimeSpan.FromSeconds(1d / Math.Clamp(_renderSettings.CaptureFrameRate, 5, 60));
        if (_captureAccumulator < captureInterval)
        {
            return;
        }

        _captureAccumulator = TimeSpan.Zero;

        int screenWidth = _captureBackend.ScreenWidth;
        int screenHeight = _captureBackend.ScreenHeight;
        if (screenWidth <= 0 || screenHeight <= 0)
        {
            return;
        }

        int destWidth = screenWidth;
        int destHeight = screenHeight;
        if (_renderSettings.EffectType == EffectType.Pixelate || _renderSettings.EffectType == EffectType.GameBoy || _renderSettings.EffectType == EffectType.VHS || _renderSettings.EffectType == EffectType.Dither)
        {
            destWidth = Math.Max((int)(screenWidth / Math.Max(_renderSettings.PixelSize, 1f)), 1);
            destHeight = Math.Max((int)(screenHeight / Math.Max(_renderSettings.PixelSize, 1f)), 1);
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
            _hasCapturedFrame = _captureBackend.TryCapture(destWidth, destHeight, _pixelBuffer);
        }
    }

    private void CanvasControl_Draw(ICanvasAnimatedControl sender, CanvasAnimatedDrawEventArgs args)
    {
        float width = (float)sender.Size.Width;
        float height = (float)sender.Size.Height;
        var drawingSession = args.DrawingSession;

        DrawCapturedScreen(sender, drawingSession, width, height);

        if (_renderSettings.EffectType == EffectType.CRT)
        {
            DrawCrtOverlay(drawingSession, width, height);
        }
        else if (_renderSettings.EffectType == EffectType.GameBoy)
        {
            DrawGameBoyOverlay(drawingSession, width, height);
        }
        else if (_renderSettings.EffectType == EffectType.VHS)
        {
            DrawVhsOverlay(drawingSession, width, height);
        }
        else if (_renderSettings.EffectType == EffectType.Dither)
        {
            DrawDitherOverlay(drawingSession, width, height);
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

        if (_renderSettings.EffectType == EffectType.Pixelate)
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

    private void DrawGameBoyOverlay(CanvasDrawingSession drawingSession, float width, float height)
    {
        var tint = _renderSettings.GameBoyDarkColor;
        drawingSession.FillRectangle(0, 0, width, height, Color.FromArgb((byte)Math.Clamp((int)(40 * _renderSettings.GameBoyBrightness), 0, 255), 9, 25, 9));
        if (_screenBitmap != null)
        {
            drawingSession.DrawImage(_screenBitmap, new Windows.Foundation.Rect(0, 0, width, height));
        }

        byte tintAlpha = (byte)Math.Clamp((int)(48 * _renderSettings.GameBoyBrightness), 0, 255);
        drawingSession.FillRectangle(0, 0, width, height, Color.FromArgb(tintAlpha, tint.R, tint.G, tint.B));

        float cellSize = Math.Clamp(_renderSettings.GameBoyPixelSize, 2f, 8f);
        for (float y = 0; y < height; y += cellSize)
        {
            for (float x = 0; x < width; x += cellSize)
            {
                float alpha = ((x + y) % (cellSize * 2)) < cellSize ? 24f : 8f;
                if (_renderSettings.GameBoyGhosting)
                {
                    alpha += 10f;
                }
                drawingSession.FillRectangle(x, y, cellSize - 1, cellSize - 1, Color.FromArgb((byte)alpha, tint.R, tint.G, tint.B));
            }
        }
    }

    private void DrawVhsOverlay(CanvasDrawingSession drawingSession, float width, float height)
    {
        if (_screenBitmap != null)
        {
            float jitter = _renderSettings.VhsGlitchAmount * (12f + (_renderSettings.VhsTrackingStrength * 18f));
            drawingSession.DrawImage(
                _screenBitmap,
                new Windows.Foundation.Rect(jitter * MathF.Sin(_scanlineY * 0.08f), 0, width + jitter, height),
                new Windows.Foundation.Rect(0, 0, _captureWidth, _captureHeight),
                1f,
                CanvasImageInterpolation.Linear);
        }

        drawingSession.FillRectangle(0, 0, width, height, Color.FromArgb((byte)(_renderSettings.VhsNoiseAmount * 90), 12, 12, 12));
        drawingSession.DrawLine(0, height * 0.88f, width, height * 0.88f, _renderSettings.VhsTintColor, Math.Max(2f, _renderSettings.VhsGlitchAmount * 8f));
        drawingSession.DrawLine(0, height * 0.92f, width, height * 0.92f, Color.FromArgb(80, 255, 80, 80), Math.Max(1f, _renderSettings.VhsTrackingStrength * 4f));
    }

    private void DrawDitherOverlay(CanvasDrawingSession drawingSession, float width, float height)
    {
        if (_screenBitmap != null)
        {
            drawingSession.DrawImage(_screenBitmap, new Windows.Foundation.Rect(0, 0, width, height));
        }

        float step = Math.Clamp(_renderSettings.DitherCellSize, 2, 12);
        for (float y = 0; y < height; y += step)
        {
            for (float x = 0; x < width; x += step)
            {
                bool dark = (((int)x + (int)y) / 4) % 2 == 0;
                drawingSession.FillRectangle(x, y, step, step, dark ? _renderSettings.DitherDarkColor : _renderSettings.DitherLightColor);
            }
        }
    }

    private void DrawCrtOverlay(CanvasDrawingSession drawingSession, float width, float height)
    {
        drawingSession.FillRectangle(0, 0, width, height, _renderSettings.CrtFilterColor);

        float spacing = Math.Max(_renderSettings.CrtScanlineWidth * 2f, 2f);
        float thickness = Math.Max(_renderSettings.CrtScanlineWidth, 1f);
        for (float y = _scanlineY % spacing; y < height; y += spacing)
        {
            drawingSession.DrawLine(0, y, width, y, _renderSettings.CrtScanlineColor, thickness);
        }
    }

    private ColorMatrixEffect CreatePixelMonochromeEffect()
    {
        float red = _renderSettings.PixelPrimaryColor.R / 255f;
        float green = _renderSettings.PixelPrimaryColor.G / 255f;
        float blue = _renderSettings.PixelPrimaryColor.B / 255f;

        return new ColorMatrixEffect
        {
            Source = _screenBitmap,
            ColorMatrix = new Matrix5x4
            {
                M11 = 0.2126f * red * _renderSettings.PixelBrightness,
                M12 = 0.2126f * green * _renderSettings.PixelBrightness,
                M13 = 0.2126f * blue * _renderSettings.PixelBrightness,
                M21 = 0.7152f * red * _renderSettings.PixelBrightness,
                M22 = 0.7152f * green * _renderSettings.PixelBrightness,
                M23 = 0.7152f * blue * _renderSettings.PixelBrightness,
                M31 = 0.0722f * red * _renderSettings.PixelBrightness,
                M32 = 0.0722f * green * _renderSettings.PixelBrightness,
                M33 = 0.0722f * blue * _renderSettings.PixelBrightness,
                M44 = 1f
            }
        };
    }

    private void SyncRenderSettings()
    {
        _renderSettings.EffectType = CurrentEffect;
        _renderSettings.CaptureFrameRate = CaptureFrameRate;
        _renderSettings.OutputFrameRate = OutputFrameRate;
        _renderSettings.IsClickThrough = IsClickThrough;
        _renderSettings.PixelSize = PixelSize;
        _renderSettings.PixelMonochrome = PixelMonochrome;
        _renderSettings.PixelPrimaryColor = ParseHexColor(PixelMonochromeColorHex, Color.FromArgb(255, 155, 188, 15));
        _renderSettings.PixelBrightness = PixelBrightness;
        _renderSettings.CrtSpeed = CrtSpeed;
        _renderSettings.CrtOpacity = CrtOpacity;
        _renderSettings.CrtMotionStrength = CrtMotionStrength;
        _renderSettings.CrtScanlineWidth = CrtScanlineWidth;
        _renderSettings.CrtFilterColor = CrtColorFilterIndex switch
        {
            0 => Color.FromArgb((byte)(CrtOpacity * 255), 255, 176, 0),
            1 => Color.FromArgb((byte)(CrtOpacity * 255), 0, 255, 0),
            2 => Color.FromArgb((byte)(CrtOpacity * 255), 0, 0, 0),
            _ => Color.FromArgb(0, 0, 0, 0)
        };
        _renderSettings.CrtScanlineColor = Color.FromArgb((byte)(CrtOpacity * 100), 0, 0, 0);
        _renderSettings.GameBoyPixelSize = GameBoyPixelSize;
        _renderSettings.GameBoyGhosting = GameBoyGhosting;
        _renderSettings.GameBoyBrightness = GameBoyBrightness;
        _renderSettings.VhsGlitchAmount = VhsGlitchAmount;
        _renderSettings.VhsNoiseAmount = VhsNoiseAmount;
        _renderSettings.VhsTintColor = ParseHexColor(VhsTintColorHex, Color.FromArgb(96, 255, 80, 80));
        _renderSettings.VhsTrackingStrength = VhsTrackingStrength;
        _renderSettings.DitherCellSize = DitherCellSize;
        byte darkAlpha = (byte)Math.Clamp((int)(DitherDarkAlpha * 255), 0, 255);
        _renderSettings.DitherDarkColor = Color.FromArgb(darkAlpha, 0, 0, 0);
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