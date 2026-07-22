using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace ScreenProtector;

public sealed partial class MainPage : Page
{
    private static OverlayWindow? _overlayWindow;
    private bool _isInitializing = true;

    public MainPage()
    {
        InitializeComponent();
        InitializeControls();
    }

    private void InitializeControls()
    {
        _isInitializing = true;

        var settings = SettingsManager.Current;

        // General
        OverlayToggle.IsOn = settings.IsOverlayEnabled;
        EffectTypeCombo.SelectedIndex = settings.EffectType;
        ClickThroughToggle.IsOn = settings.IsClickThrough;
        
        CaptureFpsSlider.Value = settings.CaptureFrameRate;
        CaptureFpsText.Text = $"{settings.CaptureFrameRate} FPS";

        OutputFpsSlider.Value = settings.OutputFrameRate;
        OutputFpsText.Text = $"{settings.OutputFrameRate} FPS";

        // Pixel Art
        PixelSizeSlider.Value = settings.PixelSize;
        PixelSizeText.Text = $"{settings.PixelSize} px";
        PixelMonoToggle.IsOn = settings.PixelMonochrome;
        CustomColorHex.Text = settings.PixelMonochromeColor;

        // CRT
        CrtIntensitySlider.Value = settings.CrtScanlineIntensity;
        CrtIntensityText.Text = $"{(int)(settings.CrtScanlineIntensity * 100)}%";
        CrtScanlineWidthSlider.Value = settings.CrtScanlineWidth;
        CrtScanlineWidthText.Text = $"{settings.CrtScanlineWidth} px";
        CrtSpeedSlider.Value = settings.CrtScanlineSpeed;
        CrtSpeedText.Text = $"{settings.CrtScanlineSpeed}";

        // Configure ComboBox selection for CRT
        int filterIndex = 0;
        switch (settings.CrtColorFilter)
        {
            case "Retro RGB": filterIndex = 0; break;
            case "None": filterIndex = 1; break;
            case "Green": filterIndex = 2; break;
            case "Amber": filterIndex = 3; break;
            case "Monochrome": filterIndex = 4; break;
        }
        CrtFilterCombo.SelectedIndex = filterIndex;

        // Toggle visibility of effect-specific cards
        UpdateCardsVisibility(settings.EffectType);

        _isInitializing = false;

        // Automatically start overlay if setting is enabled on startup
        if (settings.IsOverlayEnabled && _overlayWindow == null)
        {
            StartOverlay();
        }
    }

    private void UpdateCardsVisibility(int effectType)
    {
        if (PixelSettingsCard == null || CrtSettingsCard == null) return;

        if (effectType == 1) // Pixel Art
        {
            PixelSettingsCard.Visibility = Visibility.Visible;
            CrtSettingsCard.Visibility = Visibility.Collapsed;
        }
        else if (effectType == 2) // CRT
        {
            PixelSettingsCard.Visibility = Visibility.Collapsed;
            CrtSettingsCard.Visibility = Visibility.Visible;
        }
        else // None
        {
            PixelSettingsCard.Visibility = Visibility.Collapsed;
            CrtSettingsCard.Visibility = Visibility.Collapsed;
        }
    }

    private void StartOverlay()
    {
        if (_overlayWindow != null) return;

        try
        {
            _overlayWindow = new OverlayWindow();
            _overlayWindow.Closed += (s, e) =>
            {
                _overlayWindow = null;
                if (SettingsManager.Current.IsOverlayEnabled)
                {
                    SettingsManager.Current.IsOverlayEnabled = false;
                    OverlayToggle.IsOn = false;
                    SettingsManager.Save();
                }
            };
            _overlayWindow.Activate();
        }
        catch (Exception)
        {
            // Fail-safe
        }
    }

    private void StopOverlay()
    {
        if (_overlayWindow != null)
        {
            try
            {
                _overlayWindow.StopTimer();
                _overlayWindow.Close();
            }
            catch (Exception) { }
            _overlayWindow = null;
        }
    }

    // UI Handlers
    private void OnOverlayToggleToggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;

        SettingsManager.Current.IsOverlayEnabled = OverlayToggle.IsOn;
        SettingsManager.Save();

        if (OverlayToggle.IsOn)
        {
            StartOverlay();
        }
        else
        {
            StopOverlay();
        }
    }

    private void OnEffectTypeComboSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;

        int selectedIndex = EffectTypeCombo.SelectedIndex;
        SettingsManager.Current.EffectType = selectedIndex;
        
        // 60FPS maximum is advised for CRT scanlines to ensure a correct and smooth refresh-rate sweep feel.
        if (selectedIndex == 2)
        {
            SettingsManager.Current.OutputFrameRate = 60;
            OutputFpsSlider.Value = 60;
            OutputFpsText.Text = "60 FPS";
        }

        SettingsManager.Save();
        UpdateCardsVisibility(selectedIndex);

        if (_overlayWindow != null)
        {
            _overlayWindow.UpdateTimerInterval();
        }
    }

    private void OnClickThroughToggleToggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;

        SettingsManager.Current.IsClickThrough = ClickThroughToggle.IsOn;
        SettingsManager.Save();

        if (_overlayWindow != null)
        {
            _overlayWindow.ApplyClickThroughSettings();
        }
    }

    private void OnCaptureFpsSliderValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isInitializing) return;

        int val = (int)CaptureFpsSlider.Value;
        SettingsManager.Current.CaptureFrameRate = val;
        CaptureFpsText.Text = $"{val} FPS";
        SettingsManager.Save();
    }

    private void OnOutputFpsSliderValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isInitializing) return;

        int val = (int)OutputFpsSlider.Value;
        SettingsManager.Current.OutputFrameRate = val;
        OutputFpsText.Text = $"{val} FPS";
        SettingsManager.Save();

        if (_overlayWindow != null)
        {
            _overlayWindow.UpdateTimerInterval();
        }
    }

    private void OnPixelSizeSliderValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isInitializing) return;

        int val = (int)PixelSizeSlider.Value;
        SettingsManager.Current.PixelSize = val;
        PixelSizeText.Text = $"{val} px";
        SettingsManager.Save();
    }

    private void OnPixelMonoToggleToggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;

        bool val = PixelMonoToggle.IsOn;
        SettingsManager.Current.PixelMonochrome = val;
        SettingsManager.Save();
    }

    private void OnCrtFilterComboSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;

        string filterName = "Retro RGB";
        switch (CrtFilterCombo.SelectedIndex)
        {
            case 0: filterName = "Retro RGB"; break;
            case 1: filterName = "None"; break;
            case 2: filterName = "Green"; break;
            case 3: filterName = "Amber"; break;
            case 4: filterName = "Monochrome"; break;
        }
        SettingsManager.Current.CrtColorFilter = filterName;
        SettingsManager.Save();
    }

    private void OnCrtIntensitySliderValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isInitializing) return;

        double val = CrtIntensitySlider.Value;
        SettingsManager.Current.CrtScanlineIntensity = val;
        CrtIntensityText.Text = $"{(int)(val * 100)}%";
        SettingsManager.Save();
    }

    private void OnCrtScanlineWidthSliderValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isInitializing) return;

        int val = (int)CrtScanlineWidthSlider.Value;
        SettingsManager.Current.CrtScanlineWidth = val;
        CrtScanlineWidthText.Text = $"{val} px";
        SettingsManager.Save();
    }

    private void OnCrtSpeedSliderValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isInitializing) return;

        int val = (int)CrtSpeedSlider.Value;
        SettingsManager.Current.CrtScanlineSpeed = val;
        CrtSpeedText.Text = $"{val}";
        SettingsManager.Save();
    }

    // Color presets clicks
    private void OnGameBoyColorClick(object sender, RoutedEventArgs e)
    {
        CustomColorHex.Text = "#8BAC0F";
    }

    private void OnAmberColorClick(object sender, RoutedEventArgs e)
    {
        CustomColorHex.Text = "#FFB000";
    }

    private void OnCyanColorClick(object sender, RoutedEventArgs e)
    {
        CustomColorHex.Text = "#00FFFF";
    }

    private void OnCustomColorHexKeyUp(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        // Handled in changed event, but here to cover keystrokes
    }

    private void OnCustomColorHexChanged(object sender, TextChangedEventArgs e)
    {
        if (_isInitializing) return;

        string val = CustomColorHex.Text;
        if (!string.IsNullOrWhiteSpace(val) && val.StartsWith("#") && val.Length == 7)
        {
            SettingsManager.Current.PixelMonochromeColor = val;
            SettingsManager.Save();
        }
    }
    public static void CloseOverlayOnExit()
    {
        if (_overlayWindow != null)
        {
            try
            {
                _overlayWindow.StopTimer();
                _overlayWindow.Close();
            }
            catch (Exception) { }
            _overlayWindow = null;
        }
    }
}
