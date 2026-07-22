using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;

namespace ScreenProtector;

public sealed partial class MainWindow : Window
{
    // Basic P/Invoke for System Tray (using Shell_NotifyIcon)
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uID;
        public int uFlags;
        public int uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public int dwState;
        public int dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public int uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public int dwInfoFlags;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    public static extern bool Shell_NotifyIcon(int dwMessage, [In] ref NOTIFYICONDATA pnid);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr LoadImage(IntPtr hinst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

    private const uint IMAGE_ICON = 1;
    private const uint LR_LOADFROMFILE = 0x00000010;

    private const int NIM_ADD = 0x00000000;
    private const int NIM_MODIFY = 0x00000001;
    private const int NIM_DELETE = 0x00000002;
    private const int NIF_MESSAGE = 0x00000001;
    private const int NIF_ICON = 0x00000002;
    private const int NIF_TIP = 0x00000004;
    private const int WM_USER = 0x0400;
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_RBUTTONUP = 0x0205;

    [DllImport("user32.dll")]
    public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, WndProcDelegate dwNewLong);
    
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll")]
    public static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
    
    public delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    private const int GWLP_WNDPROC = -4;

    private NOTIFYICONDATA _trayIcon;
    private bool _runInBackground = true;
    private WndProcDelegate _wndProcDelegate;
    private IntPtr _oldWndProc;

    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        AppWindow.SetIcon("Assets/AppIcon.ico");

        RootFrame.Navigate(typeof(MainPage));

        // Setup tray icon
        SetupTrayIcon();

        // Intercept close
        this.Closed += MainWindow_Closed;
    }

    private void SetupTrayIcon()
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        
        _wndProcDelegate = TrayWndProc;
        _oldWndProc = SetWindowLongPtr(hwnd, GWLP_WNDPROC, _wndProcDelegate);

        string iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        IntPtr hIcon = LoadImage(IntPtr.Zero, iconPath, IMAGE_ICON, 0, 0, LR_LOADFROMFILE);

        _trayIcon = new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)),
            hWnd = hwnd,
            uID = 1,
            uFlags = NIF_ICON | NIF_TIP | NIF_MESSAGE,
            uCallbackMessage = WM_USER + 1,
            hIcon = hIcon,
            szTip = "ScreenProtector"
        };
        
        Shell_NotifyIcon(NIM_ADD, ref _trayIcon);
    }

    private IntPtr TrayWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_USER + 1)
        {
            int lparamInt = lParam.ToInt32();
            if (lparamInt == WM_LBUTTONUP || lparamInt == WM_RBUTTONUP)
            {
                // Dispatch back to UI thread
                DispatcherQueue.TryEnqueue(() =>
                {
                    AppWindow.Show();
                    WinRT.Interop.WindowNative.GetWindowHandle(this);
                    // Bring to front logic if necessary could go here
                });
            }
        }
        return CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        if (_runInBackground)
        {
            args.Handled = true; // Cancel close
            AppWindow.Hide();    // Hide window
        }
        else
        {
            Shell_NotifyIcon(NIM_DELETE, ref _trayIcon); // cleanup
            SetWindowLongPtr(WinRT.Interop.WindowNative.GetWindowHandle(this), GWLP_WNDPROC, _oldWndProc);
            Application.Current.Exit();
        }
    }

    public void SetRunInBackground(bool runInBackground)
    {
        _runInBackground = runInBackground;
    }
}
