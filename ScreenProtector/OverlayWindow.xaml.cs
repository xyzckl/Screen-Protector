using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.Graphics.Canvas.UI.Xaml;
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

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x20;
    private const int WS_EX_TOPMOST = 0x00000008;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;

    public OverlayWindow()
    {
        InitializeComponent();

        // Use WinUI 3 modern transparent background capability
        this.SystemBackdrop = new Microsoft.UI.Xaml.Media.TransparentTintBackdrop();

        ExtendsContentIntoTitleBar = true;

        this.Activated += OverlayWindow_Activated;
    }

    private void OverlayWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

        // Set extended window styles for click-through and toolwindow. DO NOT USE WS_EX_LAYERED with WinUI 3.
        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW);

        // Make topmost
        SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);

        // Fullscreen
        AppWindow.SetPresenter(Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen);
    }

    public enum EffectType { CRT, Pixelate }
    public EffectType CurrentEffect { get; set; } = EffectType.CRT;

    // CRT Settings
    public float CrtSpeed { get; set; } = 50f;
    public float CrtOpacity { get; set; } = 0.3f;
    public int CrtColorFilterIndex { get; set; } = 0; // 0=Amber, 1=Green, 2=None

    // Pixelate Settings
    public float PixelSize { get; set; } = 16f;
    public bool PixelMonochrome { get; set; } = false;

    private float _scanlineY = 0;

    private void CanvasControl_Update(ICanvasAnimatedControl sender, CanvasAnimatedUpdateEventArgs args)
    {
        if (CurrentEffect == EffectType.CRT)
        {
            // Move scanlines based on speed
            _scanlineY += (CrtSpeed / 10f);
            if (_scanlineY > sender.Size.Height)
            {
                _scanlineY = 0;
            }
        }
    }

    private void CanvasControl_Draw(ICanvasAnimatedControl sender, CanvasAnimatedDrawEventArgs args)
    {
        var ds = args.DrawingSession;
        var width = (float)sender.Size.Width;
        var height = (float)sender.Size.Height;

        if (CurrentEffect == EffectType.CRT)
        {
            Color filterColor = Color.FromArgb(0, 0, 0, 0);
            if (CrtColorFilterIndex == 0) // Amber
                filterColor = Color.FromArgb((byte)(CrtOpacity * 255), 255, 176, 0);
            else if (CrtColorFilterIndex == 1) // Green
                filterColor = Color.FromArgb((byte)(CrtOpacity * 255), 0, 255, 0);
            else if (CrtColorFilterIndex == 2) // None/Black
                filterColor = Color.FromArgb((byte)(CrtOpacity * 255), 0, 0, 0);

            // Draw color filter over screen
            ds.FillRectangle(0, 0, width, height, filterColor);

            // Draw moving scanlines
            var scanlineColor = Color.FromArgb((byte)(CrtOpacity * 100), 0, 0, 0);
            float spacing = 4f;
            for (float y = _scanlineY % spacing; y < height; y += spacing)
            {
                ds.DrawLine(0, y, width, y, scanlineColor, 1f);
            }
        }
        else if (CurrentEffect == EffectType.Pixelate)
        {
            // Draw a pixel grid overlay
            var gridColor = PixelMonochrome ?
                Color.FromArgb((byte)(CrtOpacity * 255), 0, 0, 0) :
                Color.FromArgb((byte)(CrtOpacity * 100), 100, 100, 100);

            for (float x = 0; x < width; x += PixelSize)
            {
                ds.DrawLine(x, 0, x, height, gridColor, 1f);
            }
            for (float y = 0; y < height; y += PixelSize)
            {
                ds.DrawLine(0, y, width, y, gridColor, 1f);
            }
        }
    }
}