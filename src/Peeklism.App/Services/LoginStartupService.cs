using Microsoft.Win32;

namespace Peeklism.App.Services;

/// <summary>
/// Starts Peeklism with Windows, per user, through the Run key.
/// </summary>
/// <remarks>
/// Peeklism is only useful while it is running: the space bar does nothing until the keyboard
/// hook is installed, and a preview tool that has to be launched first is a preview tool nobody
/// reaches for. No launch argument is needed, because Peeklism has no window to suppress.
/// </remarks>
public sealed class LoginStartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Peeklism";

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(ValueName) is string value && !string.IsNullOrWhiteSpace(value);
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
            ?? throw new InvalidOperationException("無法開啟目前使用者的登入啟動設定。");

        if (enabled)
        {
            var executable = Environment.ProcessPath
                ?? throw new InvalidOperationException("無法取得程式路徑。");
            key.SetValue(ValueName, $"\"{executable}\"");
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
