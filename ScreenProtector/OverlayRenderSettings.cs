using Windows.UI;

namespace ScreenProtector;

public sealed class OverlayRenderSettings
{
    public OverlayWindow.EffectType EffectType { get; set; } = OverlayWindow.EffectType.CRT;
    public int CaptureFrameRate { get; set; } = 30;
    public int OutputFrameRate { get; set; } = 60;
    public bool IsClickThrough { get; set; } = true;

    public float PixelSize { get; set; } = 16f;
    public bool PixelMonochrome { get; set; }
    public Color PixelPrimaryColor { get; set; } = Color.FromArgb(255, 155, 188, 15);
    public Color PixelSecondaryColor { get; set; } = Color.FromArgb(255, 15, 56, 15);
    public float PixelBrightness { get; set; } = 1f;

    public float CrtSpeed { get; set; } = 18f;
    public float CrtOpacity { get; set; } = 0.3f;
    public float CrtScanlineWidth { get; set; } = 2f;
    public Color CrtFilterColor { get; set; } = Color.FromArgb(96, 255, 176, 0);
    public Color CrtScanlineColor { get; set; } = Color.FromArgb(64, 0, 0, 0);
    public float CrtCurvature { get; set; } = 0.08f;
    public float CrtMotionStrength { get; set; } = 0.4f;

    public float GameBoyPixelSize { get; set; } = 4f;
    public bool GameBoyGhosting { get; set; } = true;
    public Color GameBoyDarkColor { get; set; } = Color.FromArgb(255, 15, 56, 15);
    public Color GameBoyLightColor { get; set; } = Color.FromArgb(255, 139, 172, 15);
    public float GameBoyBrightness { get; set; } = 1f;

    public float VhsGlitchAmount { get; set; } = 0.35f;
    public float VhsNoiseAmount { get; set; } = 0.18f;
    public Color VhsTintColor { get; set; } = Color.FromArgb(96, 255, 80, 80);
    public float VhsTrackingStrength { get; set; } = 0.35f;

    public int DitherCellSize { get; set; } = 4;
    public Color DitherDarkColor { get; set; } = Color.FromArgb(96, 0, 0, 0);
    public Color DitherLightColor { get; set; } = Color.FromArgb(0, 255, 255, 255);
    public float DitherBrightness { get; set; } = 1f;
}