using System.Diagnostics;
using Peeklism.Core.Diagnostics;

namespace Peeklism.App.Services;

internal static class ViewerLauncher
{
    public static bool Open(string? path = null)
    {
        try
        {
            var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false };
            start.ArgumentList.Add("--viewer");
            if (path is not null) { start.ArgumentList.Add("--"); start.ArgumentList.Add(path); }
            Process.Start(start);
            return true;
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or IOException or UnauthorizedAccessException)
        {
            PeekLog.Write($"viewer launch failed: {exception}");
            return false;
        }
    }
}
