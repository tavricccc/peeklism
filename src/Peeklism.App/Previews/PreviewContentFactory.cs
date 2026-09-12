using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Media.Core;

namespace Peeklism.App.Previews;

/// <summary>
/// Builds the preview for a path. Every branch is synchronous and cheap: the window has to
/// be on screen within a frame or two of the space bar, so nothing here may wait on I/O
/// beyond opening a file, and nothing may throw — an unreadable file still gets a preview
/// showing what Peeklism does know about it.
/// </summary>
public static class PreviewContentFactory
{
    /// <summary>Enough text to fill several screens without reading a multi-gigabyte log.</summary>
    private const int TextPreviewByteLimit = 256 * 1024;

    // Segoe Fluent Icons, shown in the header strip so the kind of item reads at a glance.
    private const string ImageGlyph = "";
    private const string VideoGlyph = "";
    private const string AudioGlyph = "";
    private const string TextGlyph = "";
    private const string MarkdownGlyph = "";
    private const string PdfGlyph = "";
    private const string FolderGlyph = "";
    private const string UnknownGlyph = "";
    private const string ErrorGlyph = "";

    /// <summary>Header strip height, so image sizing can account for it.</summary>
    private const int HeaderHeight = 55;

    private const int ImageMargin = 12;

    public static PreviewContent Create(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        var title = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar));
        if (string.IsNullOrEmpty(title))
        {
            title = path;
        }

        try
        {
            return FileKinds.Classify(path) switch
            {
                FileKind.Image => CreateImage(path, title),
                FileKind.Video => CreateMedia(path, title, VideoGlyph, 960, 600),
                FileKind.Audio => CreateMedia(path, title, AudioGlyph, 640, 360),
                FileKind.Text => CreateText(path, title),
                FileKind.Markdown => CreateMarkdown(path, title),
                FileKind.Pdf => CreatePdf(path, title),
                FileKind.Folder => CreateFolder(path, title),
                _ => CreateFallback(path, title),
            };
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or ArgumentException)
        {
            return new PreviewContent(
                CreateMessage($"無法預覽此項目：{exception.Message}"),
                title,
                DescribePath(path),
                ErrorGlyph,
                560,
                360);
        }
    }

    private static PreviewContent CreateImage(string path, string title)
    {
        var image = new Image
        {
            Source = new BitmapImage(new Uri(path)),
            Stretch = Stretch.Uniform,
            Margin = new Thickness(12),
        };

        var (width, height) = MeasureImage(path);
        return new PreviewContent(image, title, DescribePath(path), ImageGlyph, width, height);
    }

    /// <summary>
    /// Picks a window size matching the picture's own proportions, so a wide panorama does
    /// not open in a tall window with empty bands above and below it.
    /// </summary>
    private static (int Width, int Height) MeasureImage(string path)
    {
        const int maximumWidth = 1180;
        const int maximumHeight = 760;
        var pixels = TryReadPixelSize(path);
        if (pixels is not var (pixelWidth, pixelHeight) || pixelWidth <= 0 || pixelHeight <= 0)
        {
            return (960, 680);
        }

        var scale = Math.Min(
            Math.Min(maximumWidth / (double)pixelWidth, maximumHeight / (double)pixelHeight),
            1d);
        return (
            (int)Math.Round(pixelWidth * scale) + (ImageMargin * 2),
            (int)Math.Round(pixelHeight * scale) + (ImageMargin * 2) + HeaderHeight);
    }

    /// <summary>
    /// Reads the pixel size from the file header only.
    /// </summary>
    /// <remarks>
    /// Passing <c>validateImageData: false</c> keeps this to a header read rather than a
    /// full decode, which matters because it runs before the window is on screen. Formats
    /// GDI+ does not know — WebP, HEIC, AVIF — simply fall back to the default size.
    /// </remarks>
    private static (int Width, int Height)? TryReadPixelSize(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var image = System.Drawing.Image.FromStream(
                stream, useEmbeddedColorManagement: false, validateImageData: false);
            return (image.Width, image.Height);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or OutOfMemoryException)
        {
            // GDI+ reports an unknown format as ArgumentException or OutOfMemoryException.
            return null;
        }
    }

    private static PreviewContent CreateMedia(
        string path, string title, string glyph, int width, int height)
    {
        var player = new MediaPlayerElement
        {
            Source = MediaSource.CreateFromUri(new Uri(path)),
            AutoPlay = true,
            AreTransportControlsEnabled = true,
        };

        // Muted by default: a preview is often opened in a quiet room, and one unexpected
        // burst of sound costs more trust than the extra key press to unmute.
        player.MediaPlayer.IsMuted = true;
        return new PreviewContent(player, title, DescribePath(path), glyph, width, height);
    }

    private static PreviewContent CreateText(string path, string title)
    {
        var text = ReadTextPrefix(path, out var truncated);
        var block = new TextBlock
        {
            Text = text,
            FontFamily = new FontFamily("Cascadia Mono, Consolas, Courier New"),
            FontSize = 13,
            IsTextSelectionEnabled = true,
            TextWrapping = TextWrapping.NoWrap,
            Margin = new Thickness(16, 12, 16, 16),
        };
        var scroller = new ScrollViewer
        {
            Content = block,
            HorizontalScrollMode = ScrollMode.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };

        var subtitle = DescribePath(path);
        if (truncated)
        {
            subtitle += "　·　僅顯示開頭 256 KB";
        }

        return new PreviewContent(scroller, title, subtitle, TextGlyph, 900, 700);
    }

    private static PreviewContent CreateMarkdown(string path, string title)
    {
        var text = ReadTextPrefix(path, out var truncated);
        var scroller = new ScrollViewer
        {
            Content = MarkdownRenderer.Render(text),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollMode = ScrollMode.Disabled,
        };

        var subtitle = DescribePath(path);
        if (truncated)
        {
            subtitle += "　·　僅顯示開頭 256 KB";
        }

        // Narrow enough that prose stays at a comfortable measure rather than running the
        // full width of a wide display.
        return new PreviewContent(scroller, title, subtitle, MarkdownGlyph, 760, 740);
    }

    private static PreviewContent CreatePdf(string path, string title)
    {
        var pages = new StackPanel { Spacing = 14, Margin = new Thickness(14) };
        pages.Children.Add(new ProgressRing
        {
            IsActive = true,
            Width = 28,
            Height = 28,
            Margin = new Thickness(0, 40, 0, 0),
        });
        var scroller = new ScrollViewer
        {
            Content = pages,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollMode = ScrollMode.Disabled,
        };

        // Rendering is asynchronous by nature, so the window opens immediately and the
        // pages arrive into it. Waiting for the first page would cost the whole point of
        // pressing space.
        _ = RenderPdfPagesAsync(path, pages);
        return new PreviewContent(scroller, title, DescribePath(path), PdfGlyph, 820, 780);
    }

    /// <summary>
    /// Renders the first pages of a PDF into the panel, in order.
    /// </summary>
    /// <remarks>
    /// Only a bounded number of pages is rendered: a preview is for deciding whether this
    /// is the right document, and rasterising a 400-page report would hold the file and
    /// burn memory for something nobody is going to scroll through here.
    /// </remarks>
    private static async Task RenderPdfPagesAsync(string path, Panel host)
    {
        const uint pageLimit = 25;
        const uint renderWidth = 1400;
        try
        {
            var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(path);
            var document = await Windows.Data.Pdf.PdfDocument.LoadFromFileAsync(file);
            var count = Math.Min(document.PageCount, pageLimit);
            for (uint index = 0; index < count; index++)
            {
                using var page = document.GetPage(index);
                using var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
                await page.RenderToStreamAsync(
                    stream,
                    new Windows.Data.Pdf.PdfPageRenderOptions { DestinationWidth = renderWidth });
                stream.Seek(0);
                var bitmap = new BitmapImage();
                await bitmap.SetSourceAsync(stream);

                if (index == 0)
                {
                    host.Children.Clear();
                }

                host.Children.Add(new Border
                {
                    CornerRadius = new CornerRadius(4),
                    Background = new SolidColorBrush(Microsoft.UI.Colors.White),
                    Child = new Image { Source = bitmap, Stretch = Stretch.Uniform },
                });
            }

            if (document.PageCount > count)
            {
                host.Children.Add(new TextBlock
                {
                    Text = $"共 {document.PageCount} 頁，預覽顯示前 {count} 頁。",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                    FontSize = 12,
                    Margin = new Thickness(0, 6, 0, 10),
                });
            }
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or System.Runtime.InteropServices.COMException)
        {
            // A password-protected or damaged file surfaces here as a COM failure.
            host.Children.Clear();
            host.Children.Add(CreateMessage($"無法顯示這個 PDF：{exception.Message}"));
        }
    }

    private static PreviewContent CreateFolder(string path, string title)
    {
        var entries = new List<string>();
        var total = 0;
        foreach (var entry in Directory.EnumerateFileSystemEntries(path))
        {
            total++;
            if (entries.Count < 300)
            {
                entries.Add((Directory.Exists(entry) ? "📁  " : "📄  ") + Path.GetFileName(entry));
            }
        }

        var list = new ListView
        {
            ItemsSource = entries,
            SelectionMode = ListViewSelectionMode.None,
            Margin = new Thickness(8),
        };
        var shown = entries.Count < total ? $"　·　顯示前 {entries.Count} 項" : string.Empty;

        // A near-empty folder in a tall window looks broken, so the height follows the
        // number of rows there actually are.
        var height = Math.Clamp(110 + (entries.Count * 40), 260, 680);
        return new PreviewContent(
            list, title, $"資料夾　·　{total} 個項目{shown}", FolderGlyph, 560, height);
    }

    private static PreviewContent CreateFallback(string path, string title)
    {
        var kind = Path.GetExtension(path).TrimStart('.').ToUpperInvariant();
        var panel = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 10,
        };
        panel.Children.Add(new FontIcon
        {
            Glyph = "",
            FontSize = 48,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        });
        panel.Children.Add(new TextBlock
        {
            Text = string.IsNullOrEmpty(kind) ? "沒有可用的預覽" : $"{kind} 檔案沒有可用的預覽",
            HorizontalAlignment = HorizontalAlignment.Center,
            FontSize = 14,
        });
        return new PreviewContent(panel, title, DescribePath(path), UnknownGlyph, 560, 360);
    }

    private static TextBlock CreateMessage(string message) => new()
    {
        Text = message,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(24),
        FontWeight = FontWeights.SemiBold,
    };

    private static string ReadTextPrefix(string path, out bool truncated)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        truncated = stream.Length > TextPreviewByteLimit;
        var buffer = new byte[(int)Math.Min(stream.Length, TextPreviewByteLimit)];
        var read = stream.ReadAtLeast(buffer, buffer.Length, throwOnEndOfStream: false);
        return System.Text.Encoding.UTF8.GetString(buffer, 0, read);
    }

    private static string DescribePath(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists)
            {
                return path;
            }

            var extension = info.Extension.TrimStart('.').ToUpperInvariant();
            var kind = string.IsNullOrEmpty(extension) ? "檔案" : $"{extension} 檔案";
            return $"{kind}　·　{FormatSize(info.Length)}　·　{info.LastWriteTime:yyyy/MM/dd HH:mm}";
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException)
        {
            return path;
        }
    }

    private static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double size = bytes;
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return unit == 0 ? $"{bytes} B" : $"{size:0.#} {units[unit]}";
    }
}
