using Peeklism.Core.Viewing;
using Xunit;

namespace Peeklism.Tests.Viewing;

public sealed class ViewerTests
{
    [Fact]
    public void BackgroundLaunchDoesNotCreateViewer()
    {
        Assert.False(ViewerRequest.Parse([]).IsViewer);
        Assert.False(ViewerRequest.Parse(["--background"]).IsViewer);
    }

    [Fact]
    public void EmptyViewerLaunchIsExplicit() => Assert.True(ViewerRequest.Parse(["--viewer"]).IsViewer);

    [Fact]
    public void SettingsLaunchKeepsPreviewProcessAndOpensSettings()
    {
        var request = ViewerRequest.Parse(["--settings"]);
        Assert.False(request.IsViewer);
        Assert.True(request.OpenSettings);
        Assert.Empty(request.Paths);
    }

    [Fact]
    public void FileLaunchPreservesUnicodeSpacesAndMissingPaths()
    {
        string[] paths = [@"C:\相片\旅行 照片.png", @"C:\missing.pdf"];
        var request = ViewerRequest.Parse(paths);
        Assert.True(request.IsViewer);
        Assert.Equal(paths, request.Paths);
    }

    [Fact]
    public void LiteralSeparatorPreservesFlagLikeFileNames() =>
        Assert.Equal(["--viewer"], ViewerRequest.Parse(["--viewer", "--", "--viewer"]).Paths);

    [Theory]
    [InlineData("image.JPEG", FileKind.Image)]
    [InlineData("diagram.svg", FileKind.Image)]
    [InlineData("movie.MP4", FileKind.Video)]
    [InlineData("sound.flac", FileKind.Audio)]
    [InlineData("report.PDF", FileKind.Pdf)]
    [InlineData("README.md", FileKind.Markdown)]
    [InlineData("readme", FileKind.Text)]
    [InlineData(".gitignore", FileKind.Text)]
    [InlineData(".editorconfig", FileKind.Text)]
    [InlineData("script.cs", FileKind.Text)]
    [InlineData("app.exe", FileKind.Unknown)]
    public void ClassifiesFiles(string path, FileKind expected) => Assert.Equal(expected, FileKinds.Classify(path));

    [Fact]
    public void AllAdvertisedExtensionsHaveAViewer()
    {
        Assert.Equal(FileKinds.SupportedExtensions.Count, FileKinds.SupportedExtensions.Distinct().Count());
        foreach (var extension in FileKinds.SupportedExtensions)
            Assert.NotEqual(FileKind.Unknown, FileKinds.Classify("sample" + extension));
    }

    [Fact]
    public void MarkdownSupportsNestedListsTablesTaskListsAndCode()
    {
        var html = DocumentRenderer.Render("# Heading\n\n- outer\n  - nested\n\n- [x] checked\n\n|A|B|\n|-|-|\n|1|2|\n\n```cs\nvar a = 1;\n```", true, false);
        Assert.Contains("<h1", html);
        Assert.Contains("<table>", html);
        Assert.Contains("type=\"checkbox\"", html);
        Assert.Contains("language-cs", html);
        Assert.Contains("nested", html);
    }

    [Fact]
    public void MarkdownCannotInjectHtmlOrScripts()
    {
        var html = DocumentRenderer.Render("<script>alert(1)</script>\n\n<img src=x onerror=alert(2)>\n\n[bad](javascript:alert(3))", true, true);
        Assert.DoesNotContain("<script>", html);
        Assert.DoesNotContain("<img src=x", html);
        Assert.Contains("default-src 'none'", html);
        Assert.Contains("form-action 'none'", html);
        Assert.Contains("color-scheme:dark", html);
    }

    [Fact]
    public void TextEscapesMarkupAndIsNotTruncated()
    {
        var input = new string('a', 300_000) + "<script>END</script>";
        var html = DocumentRenderer.Render(input, false, false);
        Assert.Contains("&lt;script&gt;END&lt;/script&gt;", html);
        Assert.Contains(new string('a', 300_000), html);
    }

    [Theory]
    [InlineData("https://assets.peeklism.invalid/secret.txt")]
    [InlineData("https://evil.example/image.png")]
    [InlineData("file:///C:/secret.png")]
    [InlineData("https://assets.peeklism.invalid:8443/image.png")]
    [InlineData("https://assets.peeklism.invalid/%2e%2e%5csecret.png")]
    [InlineData("https://assets.peeklism.invalid/C%3a/secret.png")]
    [InlineData("https://assets.peeklism.invalid/image.svg")]
    public void RejectsUnsafeDocumentResources(string address) =>
        Assert.Null(LocalDocumentResources.ResolveImage(Path.GetTempPath(), new Uri(address)));

    [Fact]
    public void AllowsExistingImagesUnderDocumentDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var file = Path.Combine(directory, "圖 片.png");
            File.WriteAllBytes(file, [1, 2, 3]);
            var result = LocalDocumentResources.ResolveImage(directory,
                new Uri("https://assets.peeklism.invalid/" + Uri.EscapeDataString("圖 片.png")));
            Assert.NotNull(result);
            Assert.Equal(file, result.Value.Path);
            Assert.Equal("image/png", result.Value.ContentType);
        }
        finally { Directory.Delete(directory, true); }
    }
}
