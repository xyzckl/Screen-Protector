using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Hosting;

namespace ScreenProtector;

public sealed partial class OverlayWindow : Window
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_LAYERED = 0x00080000;
    private const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

    private readonly DispatcherTimer _renderTimer;
    private WriteableBitmap? _writeableBitmap;
    
    // Low-resolution capture buffer (extremely fast to process)
    private byte[]? _smallCaptureBuffer;
    
    // Full-screen buffers for rendering
    private byte[]? _captureBuffer;
    private byte[]? _processBuffer;
    
    private int _screenWidth;
    private int _screenHeight;
    private int _smallWidth;
    private int _smallHeight;
    
    private DateTime _lastCaptureTime = DateTime.MinValue;
    private double _sweepY = 0;

    public OverlayWindow()
    {
        InitializeComponent();

        // 1. Configure native Window behavior using AppWindow
        IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        WindowId windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        AppWindow appWindow = AppWindow.GetFromWindowId(windowId);

        appWindow.Title = "Screen Protector Overlay";
        
        // Remove standard title bar and borders to make it a borderless overlay
        var presenter = appWindow.Presenter as OverlappedPresenter;
        if (presenter != null)
        {
            presenter.IsAlwaysOnTop = true;
            presenter.IsResizable = false;
            presenter.SetBorderAndTitleBar(false, false);
        }

        // Set full screen bounds matching the primary screen
        _screenWidth = ScreenCapture.ScreenWidth;
        _screenHeight = ScreenCapture.ScreenHeight;
        appWindow.MoveAndResize(new Windows.Graphics.RectInt32(0, 0, _screenWidth, _screenHeight));

        // 2. Apply Win32 modifications: Exclude from screen capture & enable Click-Through
        SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE);
        ApplyClickThrough(hwnd, SettingsManager.Current.IsClickThrough);

        // 3. Create full-screen WriteableBitmap
        _writeableBitmap = new WriteableBitmap(_screenWidth, _screenHeight);
        OverlayImage.Source = _writeableBitmap;

        int fullBufferSize = _screenWidth * _screenHeight * 4;
        _captureBuffer = new byte[fullBufferSize];
        _processBuffer = new byte[fullBufferSize];

        // 4. Set up the rendering/updating loop
        _renderTimer = new DispatcherTimer();
        _renderTimer.Tick += OnRenderTick;
        UpdateTimerInterval();
        _renderTimer.Start();
    }

    public void UpdateTimerInterval()
    {
        var settings = SettingsManager.Current;
        _renderTimer.Interval = TimeSpan.FromMilliseconds(1000.0 / settings.OutputFrameRate);
    }

    private void ApplyClickThrough(IntPtr hwnd, bool clickThrough)
    {
        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        if (clickThrough)
        {
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED);
        }
        else
        {
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle & ~(WS_EX_TRANSPARENT | WS_EX_LAYERED));
        }
    }

    public void ApplyClickThroughSettings()
    {
        IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        ApplyClickThrough(hwnd, SettingsManager.Current.IsClickThrough);
    }

    private void OnRenderTick(object? sender, object e)
    {
        var settings = SettingsManager.Current;
        if (!settings.IsOverlayEnabled)
        {
            Close();
            return;
        }

        // Determine downsampled size based on mode
        int targetWidth, targetHeight;
        if (settings.EffectType == 1) // Pixel Art Mode
        {
            int pixelSize = Math.Max(2, settings.PixelSize);
            targetWidth = Math.Max(16, _screenWidth / pixelSize);
            targetHeight = Math.Max(9, _screenHeight / pixelSize);
        }
        else if (settings.EffectType == 2) // CRT Mode (we process at full screen but scan at half resolution for high speed)
        {
            targetWidth = _screenWidth / 2;
            targetHeight = _screenHeight / 2;
        }
        else // Normal
        {
            targetWidth = _screenWidth;
            targetHeight = _screenHeight;
        }

        // Reallocate low-res capture buffer if dimensions changed
        if (_smallCaptureBuffer == null || _smallWidth != targetWidth || _smallHeight != targetHeight)
        {
            _smallWidth = targetWidth;
            _smallHeight = targetHeight;
            _smallCaptureBuffer = new byte[_smallWidth * _smallHeight * 4];
        }

        if (_captureBuffer == null || _processBuffer == null || _smallCaptureBuffer == null) return;

        // Capture screen based on CaptureFrameRate setting
        double msPerCapture = 1000.0 / settings.CaptureFrameRate;
        if ((DateTime.Now - _lastCaptureTime).TotalMilliseconds >= msPerCapture)
        {
            // Capture the screen directly downscaled into _smallCaptureBuffer
            if (ScreenCapture.CaptureScreen(_smallWidth, _smallHeight, _smallCaptureBuffer))
            {
                _lastCaptureTime = DateTime.Now;

                // 1. Apply primary filters on the low-resolution buffer (super fast!)
                ApplyLowResFilters(settings);

                // 2. Perform Nearest-Neighbor upscale to full-screen _captureBuffer
                UpscaleToFullScreen();

                // 3. Apply full-screen specific CRT static masks (drawn sharp at monitor resolution!)
                if (settings.EffectType == 2)
                {
                    ApplyCrtFullScreenStaticFilters(settings);
                }
            }
        }

        // Apply dynamic animations (like the rolling sweep line) that update at OutputFrameRate
        if (settings.EffectType == 2) // CRT Mode sweep animation
        {
            // Copy base captured & filtered pixels to process buffer
            Array.Copy(_captureBuffer, _processBuffer, _captureBuffer.Length);

            // Animate rolling scanline sweep position
            double sweepSpeed = settings.CrtScanlineSpeed * 2.0;
            _sweepY += sweepSpeed;
            if (_sweepY >= _screenHeight)
            {
                _sweepY = 0;
            }

            ApplyCrtDynamicSweep(settings);
        }
        else
        {
            // Just copy static results directly
            Array.Copy(_captureBuffer, _processBuffer, _captureBuffer.Length);
        }

        // Render to XAML Image
        try
        {
            if (_writeableBitmap != null)
            {
                using (Stream stream = _writeableBitmap.PixelBuffer.AsStream())
                {
                    stream.Write(_processBuffer, 0, _processBuffer.Length);
                }
                _writeableBitmap.Invalidate();
            }
        }
        catch (Exception)
        {
            // Handle any lock exceptions gracefully
        }
    }

    private void ApplyLowResFilters(AppSettings settings)
    {
        if (_smallCaptureBuffer == null) return;

        int totalPixels = _smallWidth * _smallHeight;

        if (settings.EffectType == 1) // Pixel Art Filters
        {
            bool isMono = settings.PixelMonochrome;
            byte monoR = 0, monoG = 255, monoB = 0;

            if (isMono)
            {
                ParseHexColor(settings.PixelMonochromeColor, out monoR, out monoG, out monoB);
            }

            for (int i = 0; i < totalPixels; i++)
            {
                int offset = i * 4;
                byte b = _smallCaptureBuffer[offset];
                byte g = _smallCaptureBuffer[offset + 1];
                byte r = _smallCaptureBuffer[offset + 2];

                if (isMono)
                {
                    // Convert to phosphor-weighted gray, then tint with customized color
                    byte gray = (byte)(0.299 * r + 0.587 * g + 0.114 * b);
                    _smallCaptureBuffer[offset] = (byte)(gray * monoB / 255);     // B
                    _smallCaptureBuffer[offset + 1] = (byte)(gray * monoG / 255); // G
                    _smallCaptureBuffer[offset + 2] = (byte)(gray * monoR / 255); // R
                }
                else
                {
                    // retro color depth quantization (quantize to 16 steps per channel for 12-bit depth)
                    _smallCaptureBuffer[offset] = (byte)((b / 16) * 16 + 8);
                    _smallCaptureBuffer[offset + 1] = (byte)((g / 16) * 16 + 8);
                    _smallCaptureBuffer[offset + 2] = (byte)((r / 16) * 16 + 8);
                }
            }
        }
        else if (settings.EffectType == 2) // CRT Static Filter (Phosphor/Mono tint before scaling up)
        {
            string filterType = settings.CrtColorFilter;
            byte tintR = 255, tintG = 255, tintB = 255;
            bool useTint = false;

            if (filterType == "Amber")
            {
                tintR = 255; tintG = 176; tintB = 0; useTint = true;
            }
            else if (filterType == "Green")
            {
                tintR = 51; tintG = 255; tintB = 51; useTint = true;
            }

            if (filterType == "Monochrome" || filterType == "Amber" || filterType == "Green")
            {
                for (int i = 0; i < totalPixels; i++)
                {
                    int offset = i * 4;
                    byte b = _smallCaptureBuffer[offset];
                    byte g = _smallCaptureBuffer[offset + 1];
                    byte r = _smallCaptureBuffer[offset + 2];

                    byte gray = (byte)(0.299 * r + 0.587 * g + 0.114 * b);
                    if (useTint)
                    {
                        _smallCaptureBuffer[offset] = (byte)(gray * tintB / 255);
                        _smallCaptureBuffer[offset + 1] = (byte)(gray * tintG / 255);
                        _smallCaptureBuffer[offset + 2] = (byte)(gray * tintR / 255);
                    }
                    else
                    {
                        _smallCaptureBuffer[offset] = gray;
                        _smallCaptureBuffer[offset + 1] = gray;
                        _smallCaptureBuffer[offset + 2] = gray;
                    }
                }
            }
        }
    }

    private void UpscaleToFullScreen()
    {
        if (_smallCaptureBuffer == null || _captureBuffer == null) return;

        // Optimized Nearest-Neighbor upscale loop
        for (int y = 0; y < _screenHeight; y++)
        {
            int srcY = y * _smallHeight / _screenHeight;
            int srcRowOffset = srcY * _smallWidth * 4;
            int destRowOffset = y * _screenWidth * 4;

            for (int x = 0; x < _screenWidth; x++)
            {
                int srcX = x * _smallWidth / _screenWidth;
                int srcOffset = srcRowOffset + srcX * 4;
                int destOffset = destRowOffset + x * 4;

                _captureBuffer[destOffset] = _smallCaptureBuffer[srcOffset];       // B
                _captureBuffer[destOffset + 1] = _smallCaptureBuffer[srcOffset + 1]; // G
                _captureBuffer[destOffset + 2] = _smallCaptureBuffer[srcOffset + 2]; // R
                _captureBuffer[destOffset + 3] = 255;                                // A
            }
        }
    }

    private void ApplyCrtFullScreenStaticFilters(AppSettings settings)
    {
        if (_captureBuffer == null) return;

        double intensity = settings.CrtScanlineIntensity;
        double dimFactor = 1.0 - intensity;
        string filterType = settings.CrtColorFilter;
        int scanlineWidth = Math.Max(1, settings.CrtScanlineWidth);

        // Draw horizontal scanlines and Trinitron RGB slot mask on full screen for sharp pixel lines
        for (int y = 0; y < _screenHeight; y++)
        {
            bool isScanlineRow = (y % (scanlineWidth * 2)) < scanlineWidth;

            for (int x = 0; x < _screenWidth; x++)
            {
                int offset = (y * _screenWidth + x) * 4;
                byte b = _captureBuffer[offset];
                byte g = _captureBuffer[offset + 1];
                byte r = _captureBuffer[offset + 2];

                // 1. Aperture Grille RGB slot mask (Retro RGB filter)
                if (filterType == "Retro RGB")
                {
                    int subPixel = x % 3;
                    if (subPixel == 0) // Red column
                    {
                        g = (byte)(g * 0.35);
                        b = (byte)(b * 0.35);
                    }
                    else if (subPixel == 1) // Green column
                    {
                        r = (byte)(r * 0.35);
                        b = (byte)(b * 0.35);
                    }
                    else // Blue column
                    {
                        r = (byte)(r * 0.35);
                        g = (byte)(g * 0.35);
                    }
                }

                // 2. Horizontal scanlines
                if (isScanlineRow)
                {
                    b = (byte)(b * dimFactor);
                    g = (byte)(g * dimFactor);
                    r = (byte)(r * dimFactor);
                }

                _captureBuffer[offset] = b;
                _captureBuffer[offset + 1] = g;
                _captureBuffer[offset + 2] = r;
            }
        }
    }

    private void ApplyCrtDynamicSweep(AppSettings settings)
    {
        if (_processBuffer == null) return;

        // Dynamic sweep line (electron gun phosphor excitation simulation)
        int sweepCenter = (int)_sweepY;
        int sweepRadius = Math.Max(12, _screenHeight / 25); // ~4% of screen height
        double intensity = settings.CrtScanlineIntensity;

        for (int y = Math.Max(0, sweepCenter - sweepRadius); y < Math.Min(_screenHeight, sweepCenter + sweepRadius); y++)
        {
            // Gaussian-like curve for the sweep brightness excitation
            double distance = Math.Abs(y - sweepCenter);
            double factor = 1.0 + (0.45 * Math.Exp(- (distance * distance) / (2.0 * sweepRadius * sweepRadius / 4.0)));

            for (int x = 0; x < _screenWidth; x++)
            {
                int offset = (y * _screenWidth + x) * 4;
                
                // Boost brightness of pixels under the electron sweep
                int b = (int)(_processBuffer[offset] * factor);
                int g = (int)(_processBuffer[offset + 1] * factor);
                int r = (int)(_processBuffer[offset + 2] * factor);

                _processBuffer[offset] = (byte)Math.Min(255, b);
                _processBuffer[offset + 1] = (byte)Math.Min(255, g);
                _processBuffer[offset + 2] = (byte)Math.Min(255, r);
            }
        }
    }

    private void ParseHexColor(string hex, out byte r, out byte g, out byte b)
    {
        r = 0; g = 255; b = 0; // Default green
        try
        {
            if (string.IsNullOrWhiteSpace(hex)) return;
            hex = hex.Trim().Replace("#", "");
            if (hex.Length == 6)
            {
                r = Convert.ToByte(hex.Substring(0, 2), 16);
                g = Convert.ToByte(hex.Substring(2, 2), 16);
                b = Convert.ToByte(hex.Substring(4, 2), 16);
            }
        }
        catch
        {
            // Keep default
        }
    }

    public void StopTimer()
    {
        _renderTimer.Stop();
    }
}
