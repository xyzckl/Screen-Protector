using Microsoft.UI.Xaml;

namespace ScreenProtector;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        StartupTrace.Reset();
        StartupTrace.Write("Program.Main enter");

        try
        {
            StartupTrace.Write("Calling XamlCheckProcessRequirements");
            XamlCheckProcessRequirements();
            StartupTrace.Write("XamlCheckProcessRequirements completed");

            Application.Start((p) =>
            {
                StartupTrace.Write("Application.Start callback enter");
                _ = new App();
                StartupTrace.Write("App instance created");
            });
        }
        catch (Exception ex)
        {
            StartupTrace.WriteException("Program.Main", ex);
            throw;
        }
    }

    [System.Runtime.InteropServices.DllImport("Microsoft.ui.xaml.dll")]
    private static extern void XamlCheckProcessRequirements();
}