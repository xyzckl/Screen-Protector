using System;
using System.IO;
using System.Text.Json;

namespace ScreenProtector;

public class AppSettings
{
    public bool IsOverlayEnabled { get; set; } = false;
    public int EffectType { get; set; } = 1; // 0 = None, 1 = PixelArt, 2 = CRT
    public int CaptureFrameRate { get; set; } = 30; // 5 to 60 fps
    public int OutputFrameRate { get; set; } = 60; // 5 to 60 fps
    public int PixelSize { get; set; } = 8; // 2 to 32 pixels
    public bool PixelMonochrome { get; set; } = false;
    public string PixelMonochromeColor { get; set; } = "#00FF00"; // GameBoy Green, Amber, Cyan, Gray
    public int CrtScanlineSpeed { get; set; } = 3; // 0 to 10
    public double CrtScanlineIntensity { get; set; } = 0.45; // 0.0 to 1.0
    public string CrtColorFilter { get; set; } = "Retro RGB"; // None, Amber, Green, Retro RGB, Monochrome
    public int CrtScanlineWidth { get; set; } = 2; // 1 to 4 px
    public bool IsClickThrough { get; set; } = true;
}

public static class SettingsManager
{
    private static readonly string FolderPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ScreenProtector"
    );
    private static readonly string FilePath = Path.Combine(FolderPath, "settings.json");

    public static AppSettings Current { get; private set; } = new AppSettings();

    static SettingsManager()
    {
        Load();
    }

    public static void Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                string json = File.ReadAllText(FilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings != null)
                {
                    Current = settings;
                    return;
                }
            }
        }
        catch (Exception)
        {
            // Fallback to defaults on error
        }
        Current = new AppSettings();
    }

    public static void Save()
    {
        try
        {
            if (!Directory.Exists(FolderPath))
            {
                Directory.CreateDirectory(FolderPath);
            }
            string json = JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }
        catch (Exception)
        {
            // Ignore write errors gracefully
        }
    }
}
