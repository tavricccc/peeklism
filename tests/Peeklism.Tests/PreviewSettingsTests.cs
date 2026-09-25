using Peeklism.Core.Settings;
using Xunit;

namespace Peeklism.Tests;

public sealed class PreviewSettingsTests
{
    [Fact]
    public void SavesAndLoadsPreviewChoices()
    {
        var path = Path.Combine(Path.GetTempPath(), $"peeklism-settings-{Guid.NewGuid():N}.json");
        try
        {
            var settings = new PreviewSettings { AlwaysOnTop = false, PreloadPreview = true };
            settings.Save(path);
            Assert.Equal(settings, PreviewSettings.Load(path));
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void InvalidSettingsUseDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), $"peeklism-settings-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, "not json");
            Assert.Equal(new PreviewSettings(), PreviewSettings.Load(path));
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
