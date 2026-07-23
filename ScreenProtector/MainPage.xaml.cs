using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ScreenProtector;

public sealed partial class MainPage : Page
{
    public MainPage()
    {
        InitializeComponent();
        UpdateLanguage();
    }

    private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateLanguage();
    }

    private void UpdateLanguage()
    {
        bool isChinese = LanguageComboBox.SelectedIndex == 1;

        if (isChinese)
        {
            SystemSettingsTitle.Text = "系统设置";
            LanguageLabel.Text = "语言 / Language";
            ShortcutLabel.Text = "切换快捷键:";
            ShortcutTextBox.PlaceholderText = "按下按键...";
            ClearShortcutButton.Content = "清除";
            RunInBackgroundToggle.Header = "后台运行 (系统托盘)";
            RunAtStartupToggle.Header = "开机自启";
            ShowOverlayBtn.Content = "显示遮罩";
            CloseOverlayBtn.Content = "关闭遮罩";

            // Update other UI elements if needed
        }
        else
        {
            SystemSettingsTitle.Text = "System Settings";
            LanguageLabel.Text = "Language / 语言";
            ShortcutLabel.Text = "Toggle Shortcut:";
            ShortcutTextBox.PlaceholderText = "Press keys...";
            ClearShortcutButton.Content = "Clear";
            RunInBackgroundToggle.Header = "Run in Background (System Tray)";
            RunAtStartupToggle.Header = "Run at Startup";
            ShowOverlayBtn.Content = "Show Overlay";
            CloseOverlayBtn.Content = "Close Overlay";
        }
    }

    private uint _shortcutModifiers = 0;
    private uint _shortcutKey = 0;

    private void ShortcutTextBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        e.Handled = true;

        var key = e.Key;
        if (key == Windows.System.VirtualKey.Control || key == Windows.System.VirtualKey.Shift || key == Windows.System.VirtualKey.Menu)
        {
            return; // Ignore modifier only
        }

        uint modifiers = 0;
        var ctrlState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control);
        var shiftState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift);
        var altState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Menu);

        if ((ctrlState & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down) modifiers |= 0x0002; // MOD_CONTROL
        if ((shiftState & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down) modifiers |= 0x0004; // MOD_SHIFT
        if ((altState & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down) modifiers |= 0x0001; // MOD_ALT

        _shortcutModifiers = modifiers;
        _shortcutKey = (uint)key;

        string shortcutText = "";
        if ((modifiers & 0x0002) != 0) shortcutText += "Ctrl + ";
        if ((modifiers & 0x0004) != 0) shortcutText += "Shift + ";
        if ((modifiers & 0x0001) != 0) shortcutText += "Alt + ";
        shortcutText += key.ToString();

        ShortcutTextBox.Text = shortcutText;

        // Register hotkey in MainWindow
        var appWindow = (Application.Current as App)?.m_window as MainWindow;
        appWindow?.RegisterToggleHotKey(_shortcutModifiers, _shortcutKey);
    }

    private void ClearShortcutButton_Click(object sender, RoutedEventArgs e)
    {
        _shortcutModifiers = 0;
        _shortcutKey = 0;
        ShortcutTextBox.Text = "";

        var appWindow = (Application.Current as App)?.m_window as MainWindow;
        appWindow?.UnregisterToggleHotKey();
    }

    private void EffectTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CrtSettingsPanel == null || PixelSettingsPanel == null) return;

        if (EffectTypeComboBox.SelectedIndex == 0) // CRT
        {
            CrtSettingsPanel.Visibility = Visibility.Visible;
            PixelSettingsPanel.Visibility = Visibility.Collapsed;
        }
        else // Pixelate
        {
            CrtSettingsPanel.Visibility = Visibility.Collapsed;
            PixelSettingsPanel.Visibility = Visibility.Visible;
        }
        UpdateOverlaySettings();
    }

    private void CrtSettings_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        UpdateOverlaySettings();
    }

    private void CrtSettings_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateOverlaySettings();
    }

    private void PixelSettings_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        UpdateOverlaySettings();
    }

    private void PixelSettings_Toggled(object sender, RoutedEventArgs e)
    {
        UpdateOverlaySettings();
    }

    private void SystemSettings_Toggled(object sender, RoutedEventArgs e)
    {
        var appWindow = (Application.Current as App)?.m_window as MainWindow;
        if (appWindow != null)
        {
            appWindow.SetRunInBackground(RunInBackgroundToggle.IsOn);
        }
    }

    private void RunAtStartupToggle_Toggled(object sender, RoutedEventArgs e)
    {
        try
        {
            var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
            if (key != null)
            {
                if (RunAtStartupToggle.IsOn)
                {
                    string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";
                    if (!string.IsNullOrEmpty(exePath))
                    {
                        key.SetValue("ScreenProtector", exePath);
                    }
                }
                else
                {
                    key.DeleteValue("ScreenProtector", false);
                }
            }
        }
        catch (System.Exception ex)
        {
            System.Console.WriteLine($"Failed to set startup registry key: {ex.Message}");
        }
    }

    private OverlayWindow? _overlayWindow;

    public bool IsOverlayVisible => _overlayWindow != null;

    public void ShowOverlay()
    {
        if (_overlayWindow == null)
        {
            _overlayWindow = new OverlayWindow();
            _overlayWindow.Closed += (s, args) => _overlayWindow = null;
        }
        UpdateOverlaySettings();
        _overlayWindow.Activate();
    }

    public void CloseOverlay()
    {
        _overlayWindow?.Close();
        _overlayWindow = null;
    }

    private void ShowOverlay_Click(object sender, RoutedEventArgs e)
    {
        ShowOverlay();
    }

    private void CloseOverlay_Click(object sender, RoutedEventArgs e)
    {
        CloseOverlay();
    }

    private void UpdateOverlaySettings()
    {
        if (_overlayWindow == null) return;

        _overlayWindow.CurrentEffect = EffectTypeComboBox.SelectedIndex == 0 ?
            OverlayWindow.EffectType.CRT : OverlayWindow.EffectType.Pixelate;

        // CRT
        _overlayWindow.CrtSpeed = (float)CrtSpeedSlider.Value;
        _overlayWindow.CrtOpacity = (float)CrtOpacitySlider.Value;
        _overlayWindow.CrtColorFilterIndex = CrtColorComboBox.SelectedIndex;

        // Pixelate
        _overlayWindow.PixelSize = (float)PixelSizeSlider.Value;
        _overlayWindow.PixelMonochrome = PixelMonochromeToggle.IsOn;
    }
}
