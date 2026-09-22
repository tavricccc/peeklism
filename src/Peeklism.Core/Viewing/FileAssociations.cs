using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Peeklism.Core.Viewing;

/// <summary>Advertise Open with per user. Never modify UserChoice or extension default values.</summary>
public static class FileAssociations
{
    private const string ApplicationKey = @"Software\Classes\Applications\Peeklism.App.exe";
    private const string ProgId = "Peeklism.Viewer";
    private const string ProgIdKey = @"Software\Classes\" + ProgId;

    // An alternate root makes registry behavior testable without touching real associations.
    public static void Register(string executable, RegistryKey? registryRoot = null)
    {
        var root = registryRoot ?? Registry.CurrentUser;
        var command = $"\"{Path.GetFullPath(executable)}\" --viewer -- \"%1\"";
        var rollback = new Stack<Action>();
        void Set(string path, string name, object value, RegistryValueKind kind = RegistryValueKind.String)
        {
            using var previous = root.OpenSubKey(path);
            var existed = previous is not null;
            var hadValue = previous?.GetValueNames().Contains(name, StringComparer.OrdinalIgnoreCase) == true;
            var oldValue = hadValue ? previous!.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames) : null;
            var oldKind = hadValue ? previous!.GetValueKind(name) : RegistryValueKind.Unknown;
            rollback.Push(() =>
            {
                if (!existed) { root.DeleteSubKeyTree(path, false); return; }
                using var key = root.CreateSubKey(path);
                if (hadValue) key.SetValue(name, oldValue!, oldKind); else key.DeleteValue(name, false);
            });
            using var target = root.CreateSubKey(path);
            target.SetValue(name, value, kind);
        }
        try
        {
            Set(ApplicationKey, "FriendlyAppName", "Peeklism");
            Set(ApplicationKey + @"\shell\open\command", "", command);
            Set(ProgIdKey, "", "Peeklism 文件");
            Set(ProgIdKey + @"\DefaultIcon", "", $"\"{executable}\",0");
            Set(ProgIdKey + @"\shell\open\command", "", command);
            foreach (var extension in FileKinds.SupportedExtensions)
            {
                Set(ApplicationKey + @"\SupportedTypes", extension, "");
                Set(@"Software\Classes\" + extension + @"\OpenWithProgids", ProgId, Array.Empty<byte>(), RegistryValueKind.None);
            }
        }
        catch (Exception failure)
        {
            var failures = new List<Exception> { failure };
            foreach (var restore in rollback)
                try { restore(); } catch (Exception error) { failures.Add(error); }
            if (failures.Count > 1) throw new AggregateException("檔案關聯回復失敗。", failures);
            throw;
        }
        if (registryRoot is null) NotifyShell();
    }

    public static void Unregister(string executable, RegistryKey? registryRoot = null)
    {
        var root = registryRoot ?? Registry.CurrentUser;
        using (var command = root.OpenSubKey(ApplicationKey + @"\shell\open\command"))
        {
            if (command?.GetValue("") is not string value
                || !value.StartsWith($"\"{Path.GetFullPath(executable)}\" ", StringComparison.OrdinalIgnoreCase)) return;
        }
        foreach (var extension in FileKinds.SupportedExtensions)
        {
            using var key = root.OpenSubKey(@"Software\Classes\" + extension + @"\OpenWithProgids", writable: true);
            key?.DeleteValue(ProgId, false);
        }
        root.DeleteSubKeyTree(ApplicationKey, false);
        root.DeleteSubKeyTree(ProgIdKey, false);
        if (registryRoot is null) NotifyShell();
    }

    private static void NotifyShell() => SHChangeNotify(0x08000000, 0, 0, 0);

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(uint eventId, uint flags, nint item1, nint item2);
}
