using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using WinRT.Interop;

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
    private const uint LR_DEFAULTSIZE = 0x00000040;
    private const uint LR_LOADTRANSPARENT = 0x00000020;
    private const int TRAY_ICON_SIZE = 32;

    private const int NIM_ADD = 0x00000000;
    private const int NIM_MODIFY = 0x00000001;
    private const int NIM_DELETE = 0x00000002;
    private const int NIF_MESSAGE = 0x00000001;
    private const int NIF_ICON = 0x00000002;
    private const int NIF_TIP = 0x00000004;
    private const int WM_USER = 0x0400;
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_RBUTTONUP = 0x0205;
    private const int WM_COMMAND = 0x0111;
    private const int WM_HOTKEY = 0x0312;

    [DllImport("user32.dll")]
    public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, WndProcDelegate dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll")]
    public static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    public static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern ushort RegisterWindowMessage(string lpString);

    public delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    private const int GWLP_WNDPROC = -4;
    private const int SW_HIDE = 0;
    private const int SW_SHOW = 5;

    private NOTIFYICONDATA _trayIcon;
    private bool _runInBackground = true;
    private WndProcDelegate? _wndProcDelegate;
    private IntPtr _oldWndProc;
    private bool _trayInitialized;

    private const int HOTKEY_ID = 9000;

    public MainWindow()
    {
        InitializeComponent();

        AppWindow.SetIcon("Assets/AppIcon.ico");
        RootFrame.Navigate(typeof(MainPage));
        SetupTrayIcon();
        AppWindow.Closing += AppWindow_Closing;
        Closed += MainWindow_Closed;
    }

    private void SetupTrayIcon()
    {
        if (_trayInitialized)
        {
            return;
        }

        IntPtr hwnd = WindowNative.GetWindowHandle(this);
        _wndProcDelegate = TrayWndProc;
        _oldWndProc = SetWindowLongPtr(hwnd, GWLP_WNDPROC, _wndProcDelegate);

        string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        IntPtr hIcon = LoadImage(IntPtr.Zero, iconPath, IMAGE_ICON, TRAY_ICON_SIZE, TRAY_ICON_SIZE, LR_LOADFROMFILE | LR_DEFAULTSIZE | LR_LOADTRANSPARENT);

        _trayIcon = new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = hwnd,
            uID = 1,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
            uCallbackMessage = WM_USER + 1,
            hIcon = hIcon,
            szTip = "ScreenProtector"
        };

        if (!Shell_NotifyIcon(NIM_ADD, ref _trayIcon) && hIcon != IntPtr.Zero)
        {
            DestroyIcon(hIcon);
            _trayIcon.hIcon = IntPtr.Zero;
        }

        _trayInitialized = true;
    }

    private IntPtr TrayWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_HOTKEY)
        {
            if (wParam.ToInt32() == HOTKEY_ID)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    var mainPage = RootFrame.Content as MainPage;
                    if (mainPage != null)
                    {
                        if (mainPage.IsOverlayVisible)
                            mainPage.CloseOverlay();
                        else
                            mainPage.ShowOverlay();
                    }
                });
            }
        }
        else if (msg == WM_USER + 1)
        {
            int lparamInt = lParam.ToInt32();
            if (lparamInt == WM_LBUTTONUP)
            {
                // Dispatch back to UI thread
                DispatcherQueue.TryEnqueue(() =>
                {
                    ShowMainWindow();
                });
            }
            else if (lparamInt == WM_RBUTTONUP)
            {
                ShowContextMenu(hWnd);
            }
        }
        else if (msg == WM_COMMAND)
        {
            uint commandId = (uint)wParam.ToInt32() & 0xFFFF;
            DispatcherQueue.TryEnqueue(() =>
            {
                if (commandId == MENU_SHOW_OVERLAY)
                {
                    (RootFrame.Content as MainPage)?.ShowOverlay();
                }
                else if (commandId == MENU_HIDE_OVERLAY)
                {
                    (RootFrame.Content as MainPage)?.CloseOverlay();
                }
                else if (commandId == MENU_EXIT)
                {
                    _runInBackground = false;
                    this.Close();
                }
            });
        }
        return CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
    }

    private void ShowContextMenu(IntPtr hWnd)
    {
        GetCursorPos(out POINT pt);
        IntPtr hMenu = CreatePopupMenu();

        bool isOverlayVisible = (RootFrame.Content as MainPage)?.IsOverlayVisible ?? false;

        if (isOverlayVisible)
        {
            InsertMenu(hMenu, 0, MF_BYPOSITION | MF_STRING, (UIntPtr)MENU_HIDE_OVERLAY, "Close Overlay");
        }
        else
        {
            InsertMenu(hMenu, 0, MF_BYPOSITION | MF_STRING, (UIntPtr)MENU_SHOW_OVERLAY, "Show Overlay");
        }

        InsertMenu(hMenu, 1, MF_BYPOSITION | MF_STRING, (UIntPtr)MENU_EXIT, "Exit");

        SetForegroundWindow(hWnd);
        uint selectedCommand = TrackPopupMenuEx(hMenu, TPM_RETURNCMD | TPM_NONOTIFY, pt.X, pt.Y, hWnd, IntPtr.Zero);
        if (selectedCommand != 0)
        {
            PostMessage(hWnd, WM_COMMAND, (IntPtr)selectedCommand, IntPtr.Zero);
        }
        DestroyMenu(hMenu);
    }

    [DllImport("user32.dll")]
    public static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll")]
    public static extern bool InsertMenu(IntPtr hMenu, uint uPosition, uint uFlags, UIntPtr uIDNewItem, string lpNewItem);

    [DllImport("user32.dll")]
    public static extern uint TrackPopupMenuEx(IntPtr hmenu, uint fuFlags, int x, int y, IntPtr hwnd, IntPtr lptpm);

    [DllImport("user32.dll")]
    public static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    private const uint MF_BYPOSITION = 0x00000400;
    private const uint MF_STRING = 0x00000000;
    private const uint TPM_RETURNCMD = 0x0100;
    private const uint TPM_NONOTIFY = 0x0080;

    private const uint MENU_SHOW_OVERLAY = 1001;
    private const uint MENU_HIDE_OVERLAY = 1002;
    private const uint MENU_EXIT = 1003;

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        if (_trayInitialized)
        {
            Shell_NotifyIcon(NIM_DELETE, ref _trayIcon);
            if (_trayIcon.hIcon != IntPtr.Zero)
            {
                DestroyIcon(_trayIcon.hIcon);
                _trayIcon.hIcon = IntPtr.Zero;
            }

            IntPtr hwnd = WindowNative.GetWindowHandle(this);
            if (_oldWndProc != IntPtr.Zero)
            {
                SetWindowLongPtr(hwnd, GWLP_WNDPROC, _oldWndProc);
                _oldWndProc = IntPtr.Zero;
            }

            _wndProcDelegate = null;
            _trayInitialized = false;
        }

        Application.Current.Exit();
    }

    private void AppWindow_Closing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
    {
        if (_runInBackground)
        {
            args.Cancel = true;
            HideToTray();
        }
    }

    public void RegisterToggleHotKey(uint modifiers, uint key)
    {
        IntPtr hwnd = WindowNative.GetWindowHandle(this);
        UnregisterHotKey(hwnd, HOTKEY_ID);

        if (key == 0)
        {
            return;
        }

        RegisterHotKey(hwnd, HOTKEY_ID, modifiers, key);
    }

    public void UnregisterToggleHotKey()
    {
        IntPtr hwnd = WindowNative.GetWindowHandle(this);
        UnregisterHotKey(hwnd, HOTKEY_ID);
    }

    public void SetRunInBackground(bool runInBackground)
    {
        _runInBackground = runInBackground;
    }

    public void HideToTray()
    {
        IntPtr hwnd = WindowNative.GetWindowHandle(this);
        ShowWindow(hwnd, SW_HIDE);
    }

    private void ShowMainWindow()
    {
        IntPtr hwnd = WindowNative.GetWindowHandle(this);
        ShowWindow(hwnd, SW_SHOW);
        SetForegroundWindow(hwnd);
        AppWindow.Show();
    }
}
