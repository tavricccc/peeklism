using Microsoft.UI.Xaml;
using Peeklism.App.Previews;
using Peeklism.App.Services;
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
    private string? _currentPath;

    public PreviewWindow()
    {
        InitializeComponent();
        _presenter = new NoActivateWindow(this);
        _presenter.Configure();
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
        _presenter.ShowWithoutActivating();
        _presenter.HideWindow();
    }

    public void ShowFor(string path, nint sourceWindowHandle)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        var content = PreviewContentFactory.Create(path);
        TitleText.Text = content.Title;
        SubtitleText.Text = content.Subtitle;
        ContentHost.Content = content.Element;
        _currentPath = path;

        var size = MeasureFor(content, sourceWindowHandle);
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

        var content = PreviewContentFactory.Create(path);
        TitleText.Text = content.Title;
        SubtitleText.Text = content.Subtitle;
        ContentHost.Content = content.Element;
        _currentPath = path;
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
