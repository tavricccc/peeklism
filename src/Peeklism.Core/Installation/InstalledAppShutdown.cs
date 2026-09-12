using System.ComponentModel;
using System.Diagnostics;
using Peeklism.Core.Lifecycle;

namespace Peeklism.Core.Installation;

public static class InstalledAppShutdown
{
    public static void Stop(string installationDirectory)
    {
        var executable = Path.GetFullPath(Path.Combine(installationDirectory, "Peeklism.App.exe"));
        using var current = Process.GetCurrentProcess();
        foreach (var process in Process.GetProcessesByName("Peeklism.App"))
        {
            using (process)
            {
                try
                {
                    if (process.HasExited || process.SessionId != current.SessionId) continue;
                    if (!MatchesExecutable(executable, process.MainModule?.FileName)) continue;
                    // New versions release resources normally. Older or unresponsive versions
                    // are terminated after the grace period; never terminate their child apps.
                    if (AppShutdownSignal.Request(process.Id) && process.WaitForExit(2000)) continue;
                    if (!process.HasExited) process.Kill(entireProcessTree: false);
                    if (!process.WaitForExit(5000)) throw new IOException("Peeklism 尚未停止，請稍後重試更新。");
                }
                catch (InvalidOperationException) when (process.HasExited) { }
                catch (Win32Exception exception)
                {
                    throw new IOException("無法停止已安裝的 Peeklism，請確認安裝程式與 App 以相同使用者執行。", exception);
                }
            }
        }
    }

    public static bool MatchesExecutable(string expected, string? actual) => actual is not null &&
        string.Equals(Path.GetFullPath(expected), Path.GetFullPath(actual), StringComparison.OrdinalIgnoreCase);
}
