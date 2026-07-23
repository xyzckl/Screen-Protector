using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Input;

namespace ScreenProtector;

public sealed partial class MainPage : Page
{
    private const string AutoGraphicsAdapterId = "__auto__";
    private bool _isInitialized;
    private bool _isRecordingShortcut;
    private bool _isLoadingGraphicsAdapters;
    private IReadOnlyList<GraphicsAdapterInfo> _graphicsAdapters = Array.Empty<GraphicsAdapterInfo>();
    private static readonly string[] PixelMonochromeColors = ["#9BBC0F", "#FFB000", "#00C8FF", "#C8C8C8"];
    private static readonly string[] VhsTintColors = ["#FF5050", "#50A0FF", "#50FF80", "#C060FF"];
    private static readonly OverlayWindow.EffectType[] EffectTypeMap =
    [
        OverlayWindow.EffectType.CRT,
        OverlayWindow.EffectType.Pixelate,
        OverlayWindow.EffectType.GameBoy,
        OverlayWindow.EffectType.VHS,
        OverlayWindow.EffectType.Dither
    ];

    public MainPage()
    {
        StartupTrace.Write("MainPage.ctor enter");
        InitializeComponent();
        StartupTrace.Write("MainPage.InitializeComponent completed");
        Loaded += MainPage_Loaded;
    }

    private void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        StartupTrace.Write("MainPage.Loaded enter");
        Loaded -= MainPage_Loaded;
        LoadSettings();
        StartupTrace.Write("MainPage.LoadSettings completed");
        UpdateLanguage();
        StartupTrace.Write("MainPage.UpdateLanguage completed");
        SettingsNavigationView.SelectedItem = OverlayNavItem;
        UpdateVisibleSection();
        _isInitialized = true;
        UpdateOverlaySettings();
        StartupTrace.Write("MainPage.UpdateOverlaySettings completed");
        InitializeGraphicsAdapterSelector(SettingsManager.Current.PreferredGraphicsAdapterId);
        StartupTrace.Write("MainPage initialized graphics adapter selector");
    }

    private void SettingsNavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (!_isInitialized)
        {
            return;
        }

        UpdateVisibleSection();
        UpdateLanguage();
    }

    private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized) return;
        UpdateLanguage();
        SaveSettings();
    }

    private void UpdateLanguage()
    {
        bool isChinese = LanguageComboBox.SelectedIndex == 1;
        bool isOverlaySection = ((SettingsNavigationView.SelectedItem as NavigationViewItem)?.Tag as string) != "app";

        if (isChinese)
        {
            OverlayNavItem.Content = "遮罩设置";
            AppNavItem.Content = "软件设置";
            PageTitleText.Text = isOverlaySection ? "遮罩设置" : "软件设置";
            EffectTypeLabel.Text = "效果类型";
            EffectTypeComboBox.Items[0] = "CRT 扫描线";
            EffectTypeComboBox.Items[1] = "像素化";
            EffectTypeComboBox.Items[2] = "Game Boy 复古 LCD";
            EffectTypeComboBox.Items[3] = "VHS 模拟磁带";
            EffectTypeComboBox.Items[4] = "抖动网点风格";
            CrtSettingsTitle.Text = "CRT 设置";
            ScanlineSpeedLabel.Text = "扫描线速度";
            ScanlineWidthLabel.Text = "扫描线宽度";
            EffectOpacityLabel.Text = "效果不透明度";
            ColorFilterLabel.Text = "颜色滤镜";
            CrtColorComboBox.Items[0] = "经典琥珀";
            CrtColorComboBox.Items[1] = "矩阵绿色";
            CrtColorComboBox.Items[2] = "无";
            CrtMotionStrengthLabel.Text = "动态强度";
            PixelSettingsTitle.Text = "像素化设置";
            PixelSizeLabel.Text = "像素大小";
            PixelMonochromeToggle.Header = "单色模式";
            PixelMonochromeColorLabel.Text = "单色颜色";
            PixelMonochromeColorComboBox.Items[0] = "掌机绿色";
            PixelMonochromeColorComboBox.Items[1] = "琥珀";
            PixelMonochromeColorComboBox.Items[2] = "青色";
            PixelMonochromeColorComboBox.Items[3] = "灰度";
            PixelBrightnessLabel.Text = "亮度";
            GameBoyBrightnessLabel.Text = "亮度";
            VhsTintColorLabel.Text = "色偏";
            VhsTintColorComboBox.Items[0] = "红色偏移";
            VhsTintColorComboBox.Items[1] = "蓝色偏移";
            VhsTintColorComboBox.Items[2] = "绿色偏移";
            VhsTintColorComboBox.Items[3] = "紫色偏移";
            VhsTrackingStrengthLabel.Text = "磁带跟踪强度";
            DitherDarkAlphaLabel.Text = "暗部强度";
            SystemSettingsTitle.Text = "软件设置";
            LanguageLabel.Text = "语言";
            ShortcutLabel.Text = "切换快捷键";
            ShortcutTextBox.PlaceholderText = _isRecordingShortcut ? "现在按下 Ctrl / Alt / Shift + 其它键..." : "点击开始录制后按组合键...";
            RecordShortcutButton.Content = _isRecordingShortcut ? "正在录制..." : "开始录制";
            ClearShortcutButton.Content = "清除";
            RunInBackgroundToggle.Header = "后台运行 (系统托盘)";
            RunAtStartupToggle.Header = "开机自启";
            ClickThroughToggle.Header = "遮罩穿透点击";
            CaptureFrameRateLabel.Text = $"捕捉帧率: {(int)CaptureFrameRateSlider.Value} FPS";
            OutputFrameRateLabel.Text = $"输出帧率: {(int)OutputFrameRateSlider.Value} FPS";
            GraphicsAdapterLabel.Text = "渲染显卡";
            GraphicsAdapterHintText.Text = "默认使用当前显示输出显卡。需要切换时手动加载列表。";
            LoadGraphicsAdaptersButton.Content = "加载显卡列表";
            ShowOverlayBtn.Content = "显示遮罩";
            CloseOverlayBtn.Content = "关闭遮罩";
        }
        else
        {
            OverlayNavItem.Content = "Overlay";
            AppNavItem.Content = "App";
            PageTitleText.Text = isOverlaySection ? "Overlay Settings" : "App Settings";
            EffectTypeLabel.Text = "Effect Type";
            EffectTypeComboBox.Items[0] = "CRT Scanline";
            EffectTypeComboBox.Items[1] = "Pixelate";
            EffectTypeComboBox.Items[2] = "Game Boy LCD";
            EffectTypeComboBox.Items[3] = "VHS Tape";
            EffectTypeComboBox.Items[4] = "Dithering";
            CrtSettingsTitle.Text = "CRT Settings";
            ScanlineSpeedLabel.Text = "Scanline Speed";
            ScanlineWidthLabel.Text = "Scanline Width";
            EffectOpacityLabel.Text = "Effect Opacity";
            ColorFilterLabel.Text = "Color Filter";
            CrtColorComboBox.Items[0] = "Classic Amber";
            CrtColorComboBox.Items[1] = "Matrix Green";
            CrtColorComboBox.Items[2] = "None";
            CrtMotionStrengthLabel.Text = "Motion Strength";
            PixelSettingsTitle.Text = "Pixelate Settings";
            PixelSizeLabel.Text = "Pixel Size";
            PixelMonochromeToggle.Header = "Monochrome";
            PixelMonochromeColorLabel.Text = "Monochrome Color";
            PixelMonochromeColorComboBox.Items[0] = "Game Boy Green";
            PixelMonochromeColorComboBox.Items[1] = "Amber";
            PixelMonochromeColorComboBox.Items[2] = "Cyan";
            PixelMonochromeColorComboBox.Items[3] = "Gray";
            PixelBrightnessLabel.Text = "Brightness";
            GameBoyBrightnessLabel.Text = "Brightness";
            VhsTintColorLabel.Text = "Color Shift";
            VhsTintColorComboBox.Items[0] = "Red Shift";
            VhsTintColorComboBox.Items[1] = "Blue Shift";
            VhsTintColorComboBox.Items[2] = "Green Shift";
            VhsTintColorComboBox.Items[3] = "Purple Shift";
            VhsTrackingStrengthLabel.Text = "Tracking Strength";
            DitherDarkAlphaLabel.Text = "Dark Intensity";
            SystemSettingsTitle.Text = "App Settings";
            LanguageLabel.Text = "Language";
            ShortcutLabel.Text = "Toggle Shortcut";
            ShortcutTextBox.PlaceholderText = _isRecordingShortcut ? "Press Ctrl / Alt / Shift + another key now..." : "Click record, then press a shortcut...";
            RecordShortcutButton.Content = _isRecordingShortcut ? "Recording..." : "Record";
            ClearShortcutButton.Content = "Clear";
            RunInBackgroundToggle.Header = "Run in Background (System Tray)";
            RunAtStartupToggle.Header = "Run at Startup";
            ClickThroughToggle.Header = "Click-through Overlay";
            CaptureFrameRateLabel.Text = $"Capture Frame Rate: {(int)CaptureFrameRateSlider.Value} FPS";
            OutputFrameRateLabel.Text = $"Output Frame Rate: {(int)OutputFrameRateSlider.Value} FPS";
            GraphicsAdapterLabel.Text = "Rendering GPU";
            GraphicsAdapterHintText.Text = "Uses the current display GPU by default. Load the list manually only when you need to switch.";
            LoadGraphicsAdaptersButton.Content = "Load GPU List";
            ShowOverlayBtn.Content = "Show Overlay";
            CloseOverlayBtn.Content = "Close Overlay";
        }

        UpdateShortcutText();
    }

    private void LoadSettings()
    {
        var settings = SettingsManager.Current;

        LanguageComboBox.SelectedIndex = settings.Language == "en" ? 0 : 1;
        RunInBackgroundToggle.IsOn = settings.RunInBackground;
        RunAtStartupToggle.IsOn = settings.RunAtStartup;
        _shortcutModifiers = settings.ShortcutModifiers;
        _shortcutKey = settings.ShortcutKey;

        EffectTypeComboBox.SelectedIndex = settings.EffectType switch
        {
            1 => 0,
            2 => 1,
            3 => 2,
            4 => 3,
            6 => 4,
            _ => 0
        };
        CrtSpeedSlider.Value = Math.Clamp(settings.CrtScanlineSpeed, 1, 60);
        CrtScanlineWidthSlider.Value = Math.Clamp(settings.CrtScanlineWidth, 1, 4);
        CrtOpacitySlider.Value = Math.Clamp(settings.CrtScanlineIntensity, 0.0, 1.0);
        CrtColorComboBox.SelectedIndex = settings.CrtColorFilter switch
        {
            "Amber" => 0,
            "Green" => 1,
            _ => 2
        };

        PixelSizeSlider.Value = Math.Clamp(settings.PixelSize, 2, 64);
        PixelMonochromeToggle.IsOn = settings.PixelMonochrome;
        PixelMonochromeColorComboBox.SelectedIndex = Array.IndexOf(PixelMonochromeColors, settings.PixelMonochromeColor) switch
        {
            var index when index >= 0 => index,
            _ => 0
        };
        PixelBrightnessSlider.Value = Math.Clamp(settings.PixelBrightness, 0.5, 1.5);
        GameBoyPixelSizeSlider.Value = Math.Clamp(settings.GameBoyPixelSize, 2, 8);
        GameBoyGhostingToggle.IsOn = settings.GameBoyGhosting;
        GameBoyBrightnessSlider.Value = Math.Clamp(settings.GameBoyBrightness, 0.5, 1.5);
        VhsGlitchSlider.Value = Math.Clamp(settings.VhsGlitchAmount, 0f, 1f);
        VhsNoiseSlider.Value = Math.Clamp(settings.VhsNoiseAmount, 0f, 1f);
        VhsTintColorComboBox.SelectedIndex = Array.IndexOf(VhsTintColors, settings.VhsTintColor) switch
        {
            var index when index >= 0 => index,
            _ => 0
        };
        VhsTrackingStrengthSlider.Value = Math.Clamp(settings.VhsTrackingStrength, 0, 1);
        DitherCellSizeSlider.Value = Math.Clamp(settings.DitherCellSize, 2, 12);
        DitherDarkAlphaSlider.Value = Math.Clamp(settings.DitherDarkAlpha, 0, 1);
        CrtMotionStrengthSlider.Value = Math.Clamp(settings.CrtMotionStrength, 0, 1);

        CaptureFrameRateSlider.Value = Math.Clamp(settings.CaptureFrameRate, 5, 60);
        OutputFrameRateSlider.Value = Math.Clamp(settings.OutputFrameRate, 5, 60);
        ClickThroughToggle.IsOn = settings.IsClickThrough;
        UpdateEffectPanels();
        UpdateShortcutText();
    }

    private void SaveSettings()
    {
        var settings = SettingsManager.Current;
        settings.Language = LanguageComboBox.SelectedIndex == 0 ? "en" : "zh-CN";
        settings.RunInBackground = RunInBackgroundToggle.IsOn;
        settings.RunAtStartup = RunAtStartupToggle.IsOn;
        settings.ShortcutModifiers = _shortcutModifiers;
        settings.ShortcutKey = _shortcutKey;
        settings.EffectType = EffectTypeComboBox.SelectedIndex switch
        {
            0 => 2,
            1 => 1,
            2 => 3,
            3 => 4,
            4 => 6,
            _ => 1
        };
        settings.CrtScanlineSpeed = (int)Math.Round(CrtSpeedSlider.Value);
        settings.CrtScanlineWidth = (int)Math.Round(CrtScanlineWidthSlider.Value);
        settings.CrtScanlineIntensity = CrtOpacitySlider.Value;
        settings.CrtColorFilter = CrtColorComboBox.SelectedIndex switch
        {
            0 => "Amber",
            1 => "Green",
            _ => "None"
        };
        settings.PixelSize = (int)Math.Round(PixelSizeSlider.Value);
        settings.PixelMonochrome = PixelMonochromeToggle.IsOn;
        settings.PixelMonochromeColor = PixelMonochromeColors[Math.Clamp(PixelMonochromeColorComboBox.SelectedIndex, 0, PixelMonochromeColors.Length - 1)];
        settings.PixelBrightness = PixelBrightnessSlider.Value;
        settings.GameBoyPixelSize = (int)Math.Round(GameBoyPixelSizeSlider.Value);
        settings.GameBoyGhosting = GameBoyGhostingToggle.IsOn;
        settings.GameBoyBrightness = GameBoyBrightnessSlider.Value;
        settings.VhsGlitchAmount = (float)VhsGlitchSlider.Value;
        settings.VhsNoiseAmount = (float)VhsNoiseSlider.Value;
        settings.VhsTintColor = VhsTintColors[Math.Clamp(VhsTintColorComboBox.SelectedIndex, 0, VhsTintColors.Length - 1)];
        settings.VhsTrackingStrength = VhsTrackingStrengthSlider.Value;
        settings.DitherCellSize = (int)Math.Round(DitherCellSizeSlider.Value);
        settings.DitherDarkAlpha = DitherDarkAlphaSlider.Value;
        settings.CrtMotionStrength = CrtMotionStrengthSlider.Value;
        settings.CaptureFrameRate = (int)Math.Round(CaptureFrameRateSlider.Value);
        settings.OutputFrameRate = (int)Math.Round(OutputFrameRateSlider.Value);
        settings.IsClickThrough = ClickThroughToggle.IsOn;
        settings.PreferredGraphicsAdapterId = GetSelectedGraphicsAdapterId();
        settings.IsOverlayEnabled = IsOverlayVisible;
        SettingsManager.Save();
    }

    private uint _shortcutModifiers = 0;
    private uint _shortcutKey = 0;

    private void RecordShortcutButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized) return;
        _isRecordingShortcut = true;
        UpdateLanguage();
        ShortcutTextBox.Focus(FocusState.Programmatic);
        ShortcutTextBox.SelectAll();
    }

    private void ShortcutCapture_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (!_isInitialized || !_isRecordingShortcut) return;
        e.Handled = true;

        var key = e.Key;
        if (key == Windows.System.VirtualKey.Control || key == Windows.System.VirtualKey.Shift || key == Windows.System.VirtualKey.Menu)
        {
            return;
        }

        uint modifiers = 0;
        var ctrlState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control);
        var shiftState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift);
        var altState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Menu);

        if ((ctrlState & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down) modifiers |= 0x0002; // MOD_CONTROL
        if ((shiftState & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down) modifiers |= 0x0004; // MOD_SHIFT
        if ((altState & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down) modifiers |= 0x0001; // MOD_ALT

        if (modifiers == 0)
        {
            return;
        }

        _shortcutModifiers = modifiers;
        _shortcutKey = (uint)key;

        UpdateShortcutText();
        _isRecordingShortcut = false;
        UpdateLanguage();
        SaveSettings();

        var appWindow = (Application.Current as App)?.m_window as MainWindow;
        appWindow?.RegisterToggleHotKey(_shortcutModifiers, _shortcutKey);
    }

    private void ShortcutCapture_KeyUp(object sender, KeyRoutedEventArgs e)
    {
        if (_isRecordingShortcut)
        {
            e.Handled = true;
        }
    }

    private void ShortcutTextBox_LosingFocus(object sender, RoutedEventArgs e)
    {
        if (_isRecordingShortcut)
        {
            _isRecordingShortcut = false;
            UpdateLanguage();
            UpdateShortcutText();
        }
    }

    private void ClearShortcutButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized) return;
        _isRecordingShortcut = false;
        _shortcutModifiers = 0;
        _shortcutKey = 0;
        UpdateShortcutText();
        UpdateLanguage();
        SaveSettings();

        var appWindow = (Application.Current as App)?.m_window as MainWindow;
        appWindow?.UnregisterToggleHotKey();
    }

    private void EffectTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized) return;
        UpdateEffectPanels();
        UpdateOverlaySettings();
    }

    private void CrtSettings_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (!_isInitialized) return;
        UpdateOverlaySettings();
    }

    private void CrtSettings_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized) return;
        UpdateOverlaySettings();
    }

    private void PixelSettings_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized) return;
        UpdateOverlaySettings();
    }

    private void PixelSettings_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (!_isInitialized) return;
        UpdateOverlaySettings();
    }

    private void PixelSettings_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized) return;
        UpdateOverlaySettings();
    }

    private void GameBoySettings_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (!_isInitialized) return;
        UpdateOverlaySettings();
    }

    private void GameBoySettings_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized) return;
        UpdateOverlaySettings();
    }

    private void VhsSettings_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (!_isInitialized) return;
        UpdateOverlaySettings();
    }

    private void VhsSettings_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized) return;
        UpdateOverlaySettings();
    }

    private void DitherSettings_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (!_isInitialized) return;
        UpdateOverlaySettings();
    }

    private void SystemSettings_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized) return;
        var appWindow = (Application.Current as App)?.m_window as MainWindow;
        if (appWindow != null)
        {
            appWindow.SetRunInBackground(RunInBackgroundToggle.IsOn);
        }
        UpdateLanguage();
        SaveSettings();
        UpdateOverlaySettings();
    }

    private void SystemSettings_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (!_isInitialized) return;
        UpdateLanguage();
        SaveSettings();
        UpdateOverlaySettings();
    }

    private void GraphicsAdapterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized) return;
        SaveSettings();
        UpdateOverlaySettings();
    }

    private async void LoadGraphicsAdaptersButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized) return;
        await LoadGraphicsAdaptersAsync();
    }

    private void RunAtStartupToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized) return;
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
        SaveSettings();
    }

    private void UpdateVisibleSection()
    {
        bool showOverlay = ((SettingsNavigationView.SelectedItem as NavigationViewItem)?.Tag as string) != "app";
        OverlaySettingsScrollViewer.Visibility = showOverlay ? Visibility.Visible : Visibility.Collapsed;
        AppSettingsScrollViewer.Visibility = showOverlay ? Visibility.Collapsed : Visibility.Visible;
    }

    private void UpdateShortcutText()
    {
        if (_shortcutKey == 0)
        {
            ShortcutTextBox.Text = string.Empty;
            return;
        }

        string shortcutText = string.Empty;
        if ((_shortcutModifiers & 0x0002) != 0) shortcutText += "Ctrl + ";
        if ((_shortcutModifiers & 0x0004) != 0) shortcutText += "Shift + ";
        if ((_shortcutModifiers & 0x0001) != 0) shortcutText += "Alt + ";
        shortcutText += ((Windows.System.VirtualKey)_shortcutKey).ToString();
        ShortcutTextBox.Text = shortcutText;
    }

    public void CloseOverlay()
    {
        _overlayWindow?.Close();
        _overlayWindow = null;
        SaveSettings();
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
        SaveSettings();

        if (_overlayWindow == null) return;

        int effectIndex = Math.Clamp(EffectTypeComboBox.SelectedIndex, 0, EffectTypeMap.Length - 1);
        _overlayWindow.CurrentEffect = EffectTypeMap[effectIndex];

        _overlayWindow.CrtSpeed = (float)CrtSpeedSlider.Value;
        _overlayWindow.CrtOpacity = (float)CrtOpacitySlider.Value;
        _overlayWindow.CrtColorFilterIndex = CrtColorComboBox.SelectedIndex;
        _overlayWindow.CrtScanlineWidth = (float)CrtScanlineWidthSlider.Value;
        _overlayWindow.PixelSize = (float)PixelSizeSlider.Value;
        _overlayWindow.PixelMonochrome = PixelMonochromeToggle.IsOn;
        _overlayWindow.PixelMonochromeColorHex = PixelMonochromeColors[Math.Clamp(PixelMonochromeColorComboBox.SelectedIndex, 0, PixelMonochromeColors.Length - 1)];
        _overlayWindow.GameBoyPixelSize = (float)GameBoyPixelSizeSlider.Value;
        _overlayWindow.GameBoyGhosting = GameBoyGhostingToggle.IsOn;
        _overlayWindow.PixelBrightness = (float)PixelBrightnessSlider.Value;
        _overlayWindow.GameBoyBrightness = (float)GameBoyBrightnessSlider.Value;
        _overlayWindow.VhsGlitchAmount = (float)VhsGlitchSlider.Value;
        _overlayWindow.VhsNoiseAmount = (float)VhsNoiseSlider.Value;
        _overlayWindow.VhsTintColorHex = VhsTintColors[Math.Clamp(VhsTintColorComboBox.SelectedIndex, 0, VhsTintColors.Length - 1)];
        _overlayWindow.VhsTrackingStrength = (float)VhsTrackingStrengthSlider.Value;
        _overlayWindow.DitherCellSize = (int)Math.Round(DitherCellSizeSlider.Value);
        _overlayWindow.DitherDarkAlpha = (float)DitherDarkAlphaSlider.Value;
        _overlayWindow.CrtMotionStrength = (float)CrtMotionStrengthSlider.Value;
        _overlayWindow.CaptureFrameRate = (int)Math.Round(CaptureFrameRateSlider.Value);
        _overlayWindow.OutputFrameRate = (int)Math.Round(OutputFrameRateSlider.Value);
        _overlayWindow.IsClickThrough = ClickThroughToggle.IsOn;
        string selectedAdapterId = GetSelectedGraphicsAdapterId();
        if (_overlayWindow.PreferredGraphicsAdapterId != selectedAdapterId)
        {
            _overlayWindow.PreferredGraphicsAdapterId = selectedAdapterId;
            _overlayWindow.RecreateCaptureBackend();
        }
        _overlayWindow.RefreshWindowBehavior();
    }

    private void UpdateEffectPanels()
    {
        int index = EffectTypeComboBox.SelectedIndex;
        CrtSettingsPanel.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
        PixelSettingsPanel.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
        GameBoySettingsPanel.Visibility = index == 2 ? Visibility.Visible : Visibility.Collapsed;
        VhsSettingsPanel.Visibility = index == 3 ? Visibility.Visible : Visibility.Collapsed;
        DitherSettingsPanel.Visibility = index == 4 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void PopulateGraphicsAdapters(string? preferredAdapterId)
    {
        GraphicsAdapterComboBox.Items.Clear();
        GraphicsAdapterComboBox.Items.Add(CreateAutoGraphicsAdapterInfo());

        foreach (var adapter in _graphicsAdapters)
        {
            GraphicsAdapterComboBox.Items.Add(adapter);
        }

        GraphicsAdapterComboBox.IsEnabled = true;
        string? targetAdapterId = !string.IsNullOrWhiteSpace(preferredAdapterId)
            ? preferredAdapterId
            : AutoGraphicsAdapterId;

        int selectedIndex = 0;
        for (int i = 0; i < GraphicsAdapterComboBox.Items.Count; i++)
        {
            if ((GraphicsAdapterComboBox.Items[i] as GraphicsAdapterInfo)?.Id == targetAdapterId)
            {
                selectedIndex = i;
                break;
            }
        }

        GraphicsAdapterComboBox.SelectedIndex = selectedIndex;
    }

    private void PopulateGraphicsAdaptersSafe(string? preferredAdapterId)
    {
        try
        {
            PopulateGraphicsAdapters(preferredAdapterId);
        }
        catch (Exception ex)
        {
            StartupTrace.WriteException("PopulateGraphicsAdaptersSafe", ex);
            _graphicsAdapters = Array.Empty<GraphicsAdapterInfo>();
            InitializeGraphicsAdapterSelector(preferredAdapterId);
        }
    }

    private async Task LoadGraphicsAdaptersAsync()
    {
        if (_isLoadingGraphicsAdapters)
        {
            return;
        }

        _isLoadingGraphicsAdapters = true;
        string preferredAdapterId = GetSelectedGraphicsAdapterId();
        GraphicsAdapterComboBox.IsEnabled = false;
        LoadGraphicsAdaptersButton.IsEnabled = false;

        if (LanguageComboBox.SelectedIndex == 1)
        {
            GraphicsAdapterHintText.Text = "正在后台加载显卡列表...";
        }
        else
        {
            GraphicsAdapterHintText.Text = "Loading GPU list in the background...";
        }

        try
        {
            var adapters = await Task.Run(() => GraphicsAdapterService.GetAdapters());
            _graphicsAdapters = adapters;
            PopulateGraphicsAdaptersSafe(preferredAdapterId);

            if (LanguageComboBox.SelectedIndex == 1)
            {
                GraphicsAdapterHintText.Text = _graphicsAdapters.Count > 0
                    ? "显卡列表已加载，可选择用于渲染的显卡。"
                    : "未找到可用显卡，继续使用当前显示输出显卡。";
            }
            else
            {
                GraphicsAdapterHintText.Text = _graphicsAdapters.Count > 0
                    ? "GPU list loaded. You can now select the rendering GPU."
                    : "No GPU list was found. The current display GPU will continue to be used.";
            }
        }
        catch (Exception ex)
        {
            StartupTrace.WriteException("LoadGraphicsAdaptersAsync", ex);
            _graphicsAdapters = Array.Empty<GraphicsAdapterInfo>();
            InitializeGraphicsAdapterSelector(preferredAdapterId);
            GraphicsAdapterHintText.Text = LanguageComboBox.SelectedIndex == 1
                ? "显卡列表加载失败，继续使用当前显示输出显卡。"
                : "Failed to load the GPU list. The current display GPU will continue to be used.";
        }
        finally
        {
            GraphicsAdapterComboBox.IsEnabled = true;
            LoadGraphicsAdaptersButton.IsEnabled = true;
            _isLoadingGraphicsAdapters = false;
        }
    }

    private void InitializeGraphicsAdapterSelector(string? preferredAdapterId)
    {
        GraphicsAdapterComboBox.Items.Clear();
        GraphicsAdapterComboBox.Items.Add(CreateAutoGraphicsAdapterInfo());
        GraphicsAdapterComboBox.SelectedIndex = 0;
        GraphicsAdapterComboBox.IsEnabled = true;

        if (!string.IsNullOrWhiteSpace(preferredAdapterId) && preferredAdapterId != AutoGraphicsAdapterId)
        {
            GraphicsAdapterHintText.Text = LanguageComboBox.SelectedIndex == 1
                ? "已保存自定义显卡选择。点击“加载显卡列表”以恢复并切换。"
                : "A custom GPU selection is saved. Click 'Load GPU List' to restore and change it.";
        }
    }

    private GraphicsAdapterInfo CreateAutoGraphicsAdapterInfo()
    {
        return new GraphicsAdapterInfo
        {
            Id = AutoGraphicsAdapterId,
            Name = LanguageComboBox.SelectedIndex == 1 ? "自动 (当前显示输出显卡)" : "Auto (Current Display GPU)",
            IsDefaultOutputAdapter = true
        };
    }

    private string GetSelectedGraphicsAdapterId()
    {
        return (GraphicsAdapterComboBox.SelectedItem as GraphicsAdapterInfo)?.Id ?? AutoGraphicsAdapterId;
    }
}
