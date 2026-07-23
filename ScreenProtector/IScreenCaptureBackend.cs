namespace ScreenProtector;

public interface IScreenCaptureBackend : IDisposable
{
    int ScreenWidth { get; }
    int ScreenHeight { get; }
    string BackendName { get; }
    bool TryCapture(int destinationWidth, int destinationHeight, byte[] pixelBuffer);
}