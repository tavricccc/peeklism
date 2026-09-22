using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using Peeklism.Core.Viewing;

namespace Peeklism.App.Viewing;

internal sealed class DocumentViewer : IDisposable
{
    public WebView2 View { get; } = new();
    private readonly List<Stream> _streams = [];
    private readonly CancellationTokenSource _lifetime = new();
    private string _directory = "";
    private byte[] _document = [];
    private string _allowedUrl = "";
    private bool _disposed;
    private bool _pdf;
    private long _assetBytes;
    public event Action<Uri>? LinkRequested;
    public event Action<string>? Failed;
    public event Action? Loaded;

    public async Task LoadAsync(string path, bool markdown, bool source, bool dark)
    {
        _pdf = FileKinds.Classify(path) == FileKind.Pdf;
        _directory = Path.GetDirectoryName(path)!;
        if (!_pdf)
        {
            // Never silently truncate: refuse pathological text instead of presenting a partial document as complete.
            var text = await DocumentTextReader.ReadAsync(path, _lifetime.Token);
            _document = await Task.Run(() => Encoding.UTF8.GetBytes(
                DocumentRenderer.Render(text, markdown && !source, dark)), _lifetime.Token);
        }
        if (_disposed) return;
        var environment = await CoreWebView2Environment.CreateWithOptionsAsync(null,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Peeklism", "WebView2"), null);
        if (_disposed) return;
        await View.EnsureCoreWebView2Async(environment);
        if (_disposed) return;
        var core = View.CoreWebView2;
        core.Settings.IsScriptEnabled = _pdf;
        core.Settings.AreHostObjectsAllowed = false;
        core.Settings.IsWebMessageEnabled = false;
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.AreBrowserAcceleratorKeysEnabled = true;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.HiddenPdfToolbarItems = CoreWebView2PdfToolbarItems.Save | CoreWebView2PdfToolbarItems.SaveAs;
        core.Settings.IsGeneralAutofillEnabled = false;
        core.Settings.IsPasswordAutosaveEnabled = false;
        core.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
        core.DownloadStarting += (_, e) => e.Cancel = true;
        core.NewWindowRequested += (_, e) =>
        {
            e.Handled = true;
            if (e.IsUserInitiated && Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri)) LinkRequested?.Invoke(uri);
        };
        _allowedUrl = _pdf ? new Uri(path).AbsoluteUri : LocalDocumentResources.DocumentUrl;
        core.NavigationStarting += (_, e) =>
        {
            if (SameDocument(e.Uri, _allowedUrl)) return;
            e.Cancel = true;
            if (e.IsUserInitiated && Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri))
            {
                // A Markdown heading fragment is local, not an external navigation.
                if (uri.Host == LocalDocumentResources.AssetHost && uri.AbsolutePath == "/" && uri.Fragment.Length > 0)
                    core.Navigate(_allowedUrl + uri.Fragment);
                else LinkRequested?.Invoke(uri);
            }
        };
        core.NavigationCompleted += (_, e) =>
        {
            if (_disposed) return;
            if (e.IsSuccess) Loaded?.Invoke();
            else Failed?.Invoke($"文件載入失敗：{e.WebErrorStatus}。請確認檔案仍存在，然後重試。");
        };
        core.ProcessFailed += (_, _) => Failed?.Invoke("文件閱讀器已停止。請按「重試」重新載入。");
        core.AddWebResourceRequestedFilter("https://*", CoreWebView2WebResourceContext.All);
        core.AddWebResourceRequestedFilter("http://*", CoreWebView2WebResourceContext.All);
        core.WebResourceRequested += OnResourceRequested;
        core.Navigate(_allowedUrl);
    }

    private static bool SameDocument(string candidate, string allowed) =>
        Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
        && uri.GetLeftPart(UriPartial.Query).Equals(allowed, StringComparison.Ordinal);

    private async void OnResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        if (_disposed) return;
        using var deferral = e.GetDeferral();
        var environment = View.CoreWebView2.Environment;
        var token = _lifetime.Token;
        Stream? stream = null;
        string? type = null;
        try
        {
            var uri = new Uri(e.Request.Uri);
            if (!_pdf && SameDocument(e.Request.Uri, LocalDocumentResources.DocumentUrl))
            {
                stream = new MemoryStream(_document, writable: false);
                type = "text/html; charset=utf-8";
            }
            else if (!_pdf && e.ResourceContext == CoreWebView2WebResourceContext.Image && _streams.Count < 256)
            {
                // File metadata and copies can be slow on network drives; keep them off the UI thread.
                var resource = await Task.Run(() => ReadImage(uri, token), token);
                stream = resource?.Stream;
                type = resource?.Type;
            }
            if (_disposed) { stream?.Dispose(); return; }
            if (stream is null)
            {
                e.Response = environment.CreateWebResourceResponse(null, 403, "Blocked", "");
                return;
            }
            _streams.Add(stream);
            e.Response = environment.CreateWebResourceResponse(stream.AsRandomAccessStream(), 200, "OK",
                $"Content-Type: {type}\r\nCache-Control: no-store\r\nX-Content-Type-Options: nosniff");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException
            or OperationCanceledException or System.Runtime.InteropServices.COMException)
        {
            stream?.Dispose();
            if (!_disposed) e.Response = environment.CreateWebResourceResponse(null, 403, "Blocked", "");
        }
    }

    private (Stream Stream, string Type)? ReadImage(Uri uri, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (LocalDocumentResources.ResolveImage(_directory, uri) is not { } image) return null;
        using var file = new FileStream(image.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var length = file.Length;
        if (length > 32 * 1024 * 1024) return null;
        if (Interlocked.Add(ref _assetBytes, length) > 128L * 1024 * 1024)
        {
            Interlocked.Add(ref _assetBytes, -length);
            return null;
        }
        var bytes = new byte[(int)length];
        var position = 0;
        while (position < bytes.Length)
        {
            token.ThrowIfCancellationRequested();
            var read = file.Read(bytes, position, Math.Min(65536, bytes.Length - position));
            if (read == 0) break;
            position += read;
        }
        return (new MemoryStream(bytes, 0, position, writable: false), image.ContentType);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _lifetime.Cancel();
        View.Close();
        foreach (var stream in _streams) stream.Dispose();
        _streams.Clear();
        _document = [];
        _lifetime.Dispose();
    }
}
