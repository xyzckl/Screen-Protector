using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Windows.UI;

namespace ScreenProtector;

public sealed partial class OverlayWindow : Window
{
    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const uint WDA_NONE = 0x00000000;
    private const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;

    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;

    public OverlayWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        ApplyHitTestBehavior();
        Activated += OverlayWindow_Activated;
    }

    public enum EffectType { CRT, Pixelate }

    public EffectType CurrentEffect { get; set; } = EffectType.CRT;

    public float CrtSpeed { get; set; } = 18f;
    public float CrtOpacity { get; set; } = 0.3f;
    public int CrtColorFilterIndex { get; set; } = 0;
    public float CrtScanlineWidth { get; set; } = 2f;

    public float PixelSize { get; set; } = 16f;
    public bool PixelMonochrome { get; set; }
    public string PixelMonochromeColorHex { get; set; } = "#9BBC0F";

    public int CaptureFrameRate { get; set; } = 30;
    public int OutputFrameRate { get; set; } = 60;
    public bool IsClickThrough { get; set; } = true;

    private float _scanlineY;
    private byte[]? _pixelBuffer;
    private Microsoft.Graphics.Canvas.CanvasBitmap? _screenBitmap;
    private int _captureWidth;
    private int _captureHeight;
    private bool _hasCapturedFrame;
    private TimeSpan _captureAccumulator = TimeSpan.Zero;
    private Color _pixelTintColor = Color.FromArgb(255, 155, 188, 15);

    private void OverlayWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        ApplyWindowStyles(hwnd);
        ApplyHitTestBehavior();
        SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
        SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE);
        AppWindow.SetPresenter(Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen);
    }

    public void RefreshWindowBehavior()
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        ApplyWindowStyles(hwnd);
        SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE);
        ApplyHitTestBehavior();
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
        var ds = args.DrawingSession;
        float width = (float)sender.Size.Width;
        float height = (float)sender.Size.Height;

        if (_hasCapturedFrame && _pixelBuffer != null && _captureWidth > 0 && _captureHeight > 0)
        {
            if (_screenBitmap != null && (_screenBitmap.SizeInPixels.Width != _captureWidth || _screenBitmap.SizeInPixels.Height != _captureHeight))
            {
                _screenBitmap.Dispose();
                _screenBitmap = null;
            }

            if (_screenBitmap == null)
            {
                _screenBitmap = Microsoft.Graphics.Canvas.CanvasBitmap.CreateFromBytes(
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
                if (PixelMonochrome)
                {
                    var tintEffect = new ColorMatrixEffect
                    {
                        Source = _screenBitmap,
                        ColorMatrix = new Matrix5x4
                        {
                            M11 = 0.2126f * (_pixelTintColor.R / 255f),
                            M12 = 0.2126f * (_pixelTintColor.G / 255f),
                            M13 = 0.2126f * (_pixelTintColor.B / 255f),
                            M21 = 0.7152f * (_pixelTintColor.R / 255f),
                            M22 = 0.7152f * (_pixelTintColor.G / 255f),
                            M23 = 0.7152f * (_pixelTintColor.B / 255f),
                            M31 = 0.0722f * (_pixelTintColor.R / 255f),
                            M32 = 0.0722f * (_pixelTintColor.G / 255f),
                            M33 = 0.0722f * (_pixelTintColor.B / 255f),
                            M44 = 1f
                        }
                    };

                    ds.DrawImage(
                        tintEffect,
                        new Windows.Foundation.Rect(0, 0, width, height),
                        new Windows.Foundation.Rect(0, 0, _captureWidth, _captureHeight),
                        1.0f,
                        Microsoft.Graphics.Canvas.CanvasImageInterpolation.NearestNeighbor);
                }
                else
                {
                    ds.DrawImage(
                        _screenBitmap,
                        new Windows.Foundation.Rect(0, 0, width, height),
                        new Windows.Foundation.Rect(0, 0, _captureWidth, _captureHeight),
                        1.0f,
                        Microsoft.Graphics.Canvas.CanvasImageInterpolation.NearestNeighbor);
                }
            }
            else
            {
                ds.DrawImage(_screenBitmap, new Windows.Foundation.Rect(0, 0, width, height));
            }
        }

        if (CurrentEffect == EffectType.CRT)
        {
            Color filterColor = Color.FromArgb(0, 0, 0, 0);
            if (CrtColorFilterIndex == 0)
            {
                filterColor = Color.FromArgb((byte)(CrtOpacity * 255), 255, 176, 0);
            }
            else if (CrtColorFilterIndex == 1)
            {
                filterColor = Color.FromArgb((byte)(CrtOpacity * 255), 0, 255, 0);
            }
            else if (CrtColorFilterIndex == 2)
            {
                filterColor = Color.FromArgb((byte)(CrtOpacity * 255), 0, 0, 0);
            }

            ds.FillRectangle(0, 0, width, height, filterColor);

            var scanlineColor = Color.FromArgb((byte)(CrtOpacity * 100), 0, 0, 0);
            float spacing = Math.Max(CrtScanlineWidth * 2f, 2f);
            for (float y = _scanlineY % spacing; y < height; y += spacing)
            {
                ds.DrawLine(0, y, width, y, scanlineColor, Math.Max(CrtScanlineWidth, 1f));
            }
        }
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
            byte r = Convert.ToByte(normalized.Substring(0, 2), 16);
            byte g = Convert.ToByte(normalized.Substring(2, 2), 16);
            byte b = Convert.ToByte(normalized.Substring(4, 2), 16);
            return Color.FromArgb(255, r, g, b);
        }
        catch
        {
            return fallback;
        }
    }

    private void ApplyWindowStyles(IntPtr hwnd)
    {
        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        exStyle &= ~WS_EX_TRANSPARENT;

        if (IsClickThrough)
        {
            exStyle |= WS_EX_TRANSPARENT;
        }

        exStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
    }

    private void ApplyHitTestBehavior()
    {
        bool allowHitTest = !IsClickThrough;
        RootGrid.IsHitTestVisible = allowHitTest;
        CanvasControl.IsHitTestVisible = allowHitTest;
    }
}