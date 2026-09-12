using System.Diagnostics;

namespace Peeklism.Core.Diagnostics;

/// <summary>
/// Best-effort trace to a file. A launcher that lives in the tray and reacts to a global
/// key press has no console and no window to report into, so problems in the hook path are
/// otherwise invisible.
/// </summary>
public static class PeekLog
{
    private static readonly Lock Gate = new();
    private static readonly string Path = System.IO.Path.Combine(
        System.IO.Path.GetTempPath(), "peeklism.log");

    public static bool IsEnabled { get; set; } =
        Environment.GetEnvironmentVariable("PEEKLISM_LOG") == "1";

    public static string FilePath => Path;

    public static void Write(string message)
    {
        if (!IsEnabled)
        {
            return;
        }

        try
        {
            lock (Gate)
            {
                File.AppendAllText(Path, $"{DateTime.Now:HH:mm:ss.fff}  {message}{Environment.NewLine}");
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"Diagnostic logging failed: {exception}");
        }
    }
}
