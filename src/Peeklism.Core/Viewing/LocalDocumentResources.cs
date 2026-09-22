namespace Peeklism.Core.Viewing;

public static class LocalDocumentResources
{
    public const string DocumentUrl = "https://document.peeklism.invalid/index.html";
    public const string AssetHost = "assets.peeklism.invalid";

    // SVG is deliberately not embedded: external references and active content are unnecessary here.
    private static readonly Dictionary<string, string> ImageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png", [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif", [".webp"] = "image/webp", [".bmp"] = "image/bmp",
        [".ico"] = "image/x-icon", [".avif"] = "image/avif",
    };

    public static (string Path, string ContentType)? ResolveImage(string directory, Uri uri)
    {
        if (uri.Scheme != "https" || uri.Host != AssetHost || !uri.IsDefaultPort) return null;
        var relative = Uri.UnescapeDataString(uri.AbsolutePath).TrimStart('/');
        if (relative.Contains(':') || relative.Contains('\\') || relative.Contains('\0')) return null;
        try
        {
            var root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var path = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                || !ImageTypes.TryGetValue(Path.GetExtension(path), out var type)) return null;
            // Never follow a junction/symlink, including one in the document root's ancestors.
            for (var current = path; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return null;
            }
            return (path, type);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }
}
