using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ScreenProtector;

public sealed partial class MainPage : Page
{
    public MainPage()
    {
        InitializeComponent();
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

    private void ShowOverlay_Click(object sender, RoutedEventArgs e)
    {
        if (_overlayWindow == null)
        {
            _overlayWindow = new OverlayWindow();
            _overlayWindow.Closed += (s, args) => _overlayWindow = null;
        }
        UpdateOverlaySettings();
        _overlayWindow.Activate();
    }

    private void CloseOverlay_Click(object sender, RoutedEventArgs e)
    {
        _overlayWindow?.Close();
        _overlayWindow = null;
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
