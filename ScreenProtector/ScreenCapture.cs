using System;
using System.Runtime.InteropServices;

namespace ScreenProtector;

public static class ScreenCapture
{
    private const int SRCCOPY = 0x00CC0020;
    private const int DIB_RGB_COLORS = 0;
    private const int BI_RGB = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFO
    {
        public BITMAPINFOHEADER bmiHeader;
        public uint bmiColors;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hDC, int nWidth, int nHeight);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hDestDC, int x, int y, int nWidth, int nHeight, IntPtr hSrcDC, int xSrc, int ySrc, int dwRop);

    [DllImport("gdi32.dll")]
    private static extern bool StretchBlt(IntPtr hDestDC, int x, int y, int nWidth, int nHeight, IntPtr hSrcDC, int xSrc, int ySrc, int nSrcWidth, int nSrcHeight, int dwRop);

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, uint uStartScan, uint cScanLines, [Out] byte[] lpvBits, ref BITMAPINFO lpbmi, uint uUsage);

    [DllImport("user32.dll")]
    public static extern int GetSystemMetrics(int nIndex);

    private static readonly Lazy<int> _screenWidth = new Lazy<int>(() => GetSystemMetrics(0));
    private static readonly Lazy<int> _screenHeight = new Lazy<int>(() => GetSystemMetrics(1));

    public static int ScreenWidth => _screenWidth.Value;  // SM_CXSCREEN
    public static int ScreenHeight => _screenHeight.Value; // SM_CYSCREEN

    /// <summary>
    /// Captures the primary screen into a BGRA byte array.
    /// Uses GDI StretchBlt for ultra-fast downsampling if destWidth and destHeight are smaller than screen resolution.
    /// </summary>
    public static bool CaptureScreen(int destWidth, int destHeight, byte[] pixelBuffer)
    {
        if (pixelBuffer == null || pixelBuffer.Length < destWidth * destHeight * 4)
            return false;

        IntPtr hdcScreen = IntPtr.Zero;
        IntPtr hdcMem = IntPtr.Zero;
        IntPtr hBitmap = IntPtr.Zero;
        IntPtr hOldBitmap = IntPtr.Zero;

        try
        {
            int srcWidth = ScreenWidth;
            int srcHeight = ScreenHeight;

            hdcScreen = GetDC(IntPtr.Zero);
            if (hdcScreen == IntPtr.Zero) return false;

            hdcMem = CreateCompatibleDC(hdcScreen);
            if (hdcMem == IntPtr.Zero) return false;

            hBitmap = CreateCompatibleBitmap(hdcScreen, destWidth, destHeight);
            if (hBitmap == IntPtr.Zero) return false;

            hOldBitmap = SelectObject(hdcMem, hBitmap);

            // If we are downsampling (Pixel Art mode), StretchBlt performs hardware-accelerated bilinear/box resizing in GDI driver
            if (destWidth == srcWidth && destHeight == srcHeight)
            {
                if (!BitBlt(hdcMem, 0, 0, destWidth, destHeight, hdcScreen, 0, 0, SRCCOPY))
                    return false;
            }
            else
            {
                if (!StretchBlt(hdcMem, 0, 0, destWidth, destHeight, hdcScreen, 0, 0, srcWidth, srcHeight, SRCCOPY))
                    return false;
            }

            BITMAPINFO bmi = new BITMAPINFO();
            bmi.bmiHeader.biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>();
            bmi.bmiHeader.biWidth = destWidth;
            // biHeight is negative to specify a top-down DIB (origin at top-left, instead of bottom-left)
            bmi.bmiHeader.biHeight = -destHeight;
            bmi.bmiHeader.biPlanes = 1;
            bmi.bmiHeader.biBitCount = 32; // 32-bit BGRA
            bmi.bmiHeader.biCompression = BI_RGB;

            int result = GetDIBits(hdcMem, hBitmap, 0, (uint)destHeight, pixelBuffer, ref bmi, DIB_RGB_COLORS);
            return result > 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return false;
        }
        finally
        {
            if (hOldBitmap != IntPtr.Zero && hdcMem != IntPtr.Zero)
                SelectObject(hdcMem, hOldBitmap);

            if (hBitmap != IntPtr.Zero)
                DeleteObject(hBitmap);

            if (hdcMem != IntPtr.Zero)
                DeleteDC(hdcMem);

            if (hdcScreen != IntPtr.Zero)
                ReleaseDC(IntPtr.Zero, hdcScreen);
        }
    }
}
