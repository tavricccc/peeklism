namespace Peeklism.Core.Viewing;

public enum FileKind
{
    Unknown,
    Image,
    Video,
    Audio,
    Text,
    Markdown,
    Pdf,
    Folder,
}

public static class FileKinds
{
    private static readonly HashSet<string> Images = new(StringComparer.OrdinalIgnoreCase)
    {
        // Some formats require an optional Windows codec; decode failures are shown in the viewer.
        ".png", ".jpg", ".jpeg", ".jfif", ".gif", ".bmp", ".webp", ".heic", ".heif",
        ".tif", ".tiff", ".ico", ".avif", ".dng", ".svg",
    };

    private static readonly HashSet<string> Videos = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".m4v", ".mov", ".mkv", ".avi", ".wmv", ".webm",
    };

    private static readonly HashSet<string> Audio = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".m4a", ".wav", ".flac", ".aac", ".wma", ".ogg", ".opus",
    };

    private static readonly HashSet<string> Markdown = new(StringComparer.OrdinalIgnoreCase)
    {
        ".md", ".markdown", ".mdx",
    };

    private static readonly HashSet<string> Text = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".log", ".json", ".xml", ".yml", ".yaml", ".ini", ".cfg",
        ".csv", ".tsv", ".html", ".htm", ".css", ".js", ".ts", ".tsx", ".jsx", ".cs", ".c",
        ".h", ".cpp", ".hpp", ".py", ".rb", ".go", ".rs", ".java", ".kt", ".swift", ".sh",
        ".ps1", ".bat", ".sql", ".toml", ".gitignore", ".editorconfig", ".props", ".targets",
        ".csproj", ".sln", ".slnx", ".config",
    };

    public static IReadOnlyList<string> SupportedExtensions { get; } =
        Images.Concat(Videos).Concat(Audio).Concat(Markdown).Concat(Text).Append(".pdf")
            .Order(StringComparer.OrdinalIgnoreCase).ToArray();

    public static FileKind Classify(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (Directory.Exists(path))
        {
            return FileKind.Folder;
        }

        var name = Path.GetFileName(path);
        if (name.Equals(".gitignore", StringComparison.OrdinalIgnoreCase)
            || name.Equals(".editorconfig", StringComparison.OrdinalIgnoreCase)
            || name.Equals("LICENSE", StringComparison.OrdinalIgnoreCase)
            || name.Equals("README", StringComparison.OrdinalIgnoreCase)) return FileKind.Text;
        var extension = Path.GetExtension(path);
        if (string.IsNullOrEmpty(extension))
        {
            return FileKind.Unknown;
        }

        if (Markdown.Contains(extension)) return FileKind.Markdown;
        if (extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase)) return FileKind.Pdf;
        if (Images.Contains(extension)) return FileKind.Image;
        if (Videos.Contains(extension)) return FileKind.Video;
        if (Audio.Contains(extension)) return FileKind.Audio;
        if (Text.Contains(extension)) return FileKind.Text;
        return FileKind.Unknown;
    }
}
