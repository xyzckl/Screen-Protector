using System;
using System.IO;

namespace ScreenProtector;

internal static class StartupTrace
{
    private static readonly object Sync = new();
    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ScreenProtector");
    private static readonly string LogPath = Path.Combine(LogDirectory, "startup.log");

    public static string CurrentLogPath => LogPath;

    public static void Reset()
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            File.WriteAllText(LogPath, $"=== Startup Trace {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} ==={Environment.NewLine}");
        }
        catch
        {
        }
    }

    public static void Write(string message)
    {
        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(LogDirectory);
                File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}");
            }
        }
        catch
        {
        }
    }

    public static void WriteException(string stage, Exception exception)
    {
        Write($"{stage} failed: {exception}");
    }
}