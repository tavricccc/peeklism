using Microsoft.Win32;
using Peeklism.Core.Viewing;
using Xunit;

namespace Peeklism.Tests.Viewing;

public sealed class FileAssociationTests : IDisposable
{
    private readonly string _keyPath = @"Software\Peeklism.Tests\" + Guid.NewGuid().ToString("N");
    private readonly RegistryKey _root;
    public FileAssociationTests() => _root = Registry.CurrentUser.CreateSubKey(_keyPath);

    [Fact]
    public void RegisterQuotesUnicodeAndSpacePathsWithoutChangingDefaults()
    {
        const string executable = @"C:\測試 目錄\Peeklism.App.exe";
        using (var extension = _root.CreateSubKey(@"Software\Classes\.pdf")) extension.SetValue("", "Existing.PdfApp");
        FileAssociations.Register(executable, _root);
        using var command = _root.OpenSubKey(@"Software\Classes\Peeklism.Viewer\shell\open\command");
        Assert.Equal($"\"{executable}\" --viewer -- \"%1\"", command!.GetValue(""));
        using var pdf = _root.OpenSubKey(@"Software\Classes\.pdf");
        Assert.Equal("Existing.PdfApp", pdf!.GetValue(""));
        using var candidates = pdf.OpenSubKey("OpenWithProgids");
        Assert.Contains("Peeklism.Viewer", candidates!.GetValueNames());
        Assert.Equal(RegistryValueKind.None, candidates.GetValueKind("Peeklism.Viewer"));
    }

    [Fact]
    public void UnregisterKeepsOtherAppsAndDoesNotRemoveNewerInstallation()
    {
        const string oldPath = @"C:\Old\Peeklism.App.exe";
        const string newPath = @"C:\New\Peeklism.App.exe";
        FileAssociations.Register(oldPath, _root);
        FileAssociations.Register(newPath, _root);
        using (var candidates = _root.CreateSubKey(@"Software\Classes\.pdf\OpenWithProgids"))
            candidates.SetValue("Other.Viewer", Array.Empty<byte>(), RegistryValueKind.None);
        FileAssociations.Unregister(oldPath, _root);
        using (var app = _root.OpenSubKey(@"Software\Classes\Applications\Peeklism.App.exe")) Assert.NotNull(app);
        FileAssociations.Unregister(newPath, _root);
        using var removed = _root.OpenSubKey(@"Software\Classes\Applications\Peeklism.App.exe");
        Assert.Null(removed);
        using var remaining = _root.OpenSubKey(@"Software\Classes\.pdf\OpenWithProgids");
        Assert.Equal(["Other.Viewer"], remaining!.GetValueNames());
    }

    public void Dispose()
    {
        _root.Dispose();
        Registry.CurrentUser.DeleteSubKeyTree(_keyPath, false);
    }
}
