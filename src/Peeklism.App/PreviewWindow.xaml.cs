using System.Diagnostics;
using Microsoft.UI.Xaml;
using Peeklism.App.Previews;
using Peeklism.App.Services;
using Peeklism.Core.Diagnostics;
using Windows.Graphics;

namespace Peeklism.App;

/// <summary>
/// The preview surface. It never takes focus: File Explorer keeps the keyboard, so the
/// arrow keys, rename, copy and the context menu all keep working while a preview is up,
/// and the selection the user moves to is simply reflected here.
/// </summary>
public sealed partial class PreviewWindow : Window
{
    private const int MinimumWidth = 480;
    private const int MinimumHeight = 360;
    private readonly NoActivateWindow _presenter;
    private readonly AlwaysActiveBackdrop _backdrop = new();
    private string? _currentPath;

    public PreviewWindow()
    {
        InitializeComponent();
        _presenter = new NoActivateWindow(this);
        _presenter.Configure();
        _backdrop.TryApply(this);
    }

    public bool IsShowing { get; private set; }

    public string? CurrentPath => _currentPath;

    /// <summary>
    /// Renders the window once, off screen, so the first real preview pays no XAML warm-up
    /// cost. Peeklism lives in the tray, which makes this a one-time price at start-up.
    /// </summary>
    public void WarmUp()
    {
        _presenter.MoveOffScreen();

        // Showing once is what makes WinUI build the content. WS_EX_NOACTIVATE is already
        // in place, so this costs no focus.
        _presenter.ShowWithoutActivating();
        _presenter.HideWindow();
        PeekLog.Write("preview window warmed up");
    }

    public void ShowFor(string path, nint sourceWindowHandle)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        var content = PreviewContentFactory.Create(path);
        Apply(content, path);

        var size = MeasureFor(content, sourceWindowHandle);
        PeekLog.Write($"showing preview: {path} at {size.Width}x{size.Height}");
        _presenter.CenterOn(sourceWindowHandle, size);
        _presenter.ShowWithoutActivating();
        IsShowing = true;
    }

    /// <summary>
    /// Swaps in another file while the window stays exactly where it is. Re-centring or
    /// resizing on every arrow key would make arrowing through a folder unusable.
    /// </summary>
    public void UpdateContent(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (!IsShowing)
        {
            return;
        }

        Apply(PreviewContentFactory.Create(path), path);
    }

    private void Apply(PreviewContent content, string path)
    {
        TitleText.Text = content.Title;
        SubtitleText.Text = content.Subtitle;
        KindIcon.Glyph = content.Glyph;
        ContentHost.Content = content.Element;
        _currentPath = path;
    }

    /// <summary>
    /// Opens the previewed item with its default application. The window takes no focus, so
    /// a click reaches the button without the shell ever losing the keyboard.
    /// </summary>
    private void OnOpenClick(object sender, RoutedEventArgs args)
    {
        var path = _currentPath;
        HidePreview();
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception
            or IOException
            or UnauthorizedAccessException)
        {
            PeekLog.Write($"opening {path} failed: {exception.Message}");
        }
    }

    public void HidePreview()
    {
        if (!IsShowing)
        {
            return;
        }

        IsShowing = false;
        _currentPath = null;
        // Drop the content so a decoded bitmap or a playing media element does not sit in
        // memory while the window is invisible.
        ContentHost.Content = null;
        _presenter.HideWindow();
    }

    private SizeInt32 MeasureFor(PreviewContent content, nint sourceWindowHandle)
    {
        var bounds = _presenter.GetWorkAreaFor(sourceWindowHandle);
        var maximumWidth = (int)(bounds.Width * 0.8);
        var maximumHeight = (int)(bounds.Height * 0.8);
        return new SizeInt32(
            Math.Clamp(content.PreferredWidth, MinimumWidth, Math.Max(MinimumWidth, maximumWidth)),
            Math.Clamp(content.PreferredHeight, MinimumHeight, Math.Max(MinimumHeight, maximumHeight)));
    }
}
