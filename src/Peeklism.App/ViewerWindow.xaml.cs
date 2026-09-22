using System.Diagnostics;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Peeklism.App.Viewing;
using Peeklism.Core.Viewing;
using Windows.ApplicationModel.DataTransfer;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage.Pickers;
using Windows.System;

namespace Peeklism.App;

/// <summary>A normal, focusable window. No preview controller, keyboard hook or no-activate style.</summary>
public sealed partial class ViewerWindow : Window
{
    private ImageViewer? _image;
    private DocumentViewer? _document;
    private MediaPlayer? _player;
    private MediaSource? _mediaSource;
    private MediaPlayerElement? _mediaElement;
    private string? _path;
    private List<string> _playlist = [];
    private int _generation;
    private bool _closed;
    private bool _dialogOpen;
    private bool _picking;
    private FileKind _kind;

    public ViewerWindow(IReadOnlyList<string> paths)
    {
        InitializeComponent();
        SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop();
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1180, 820));
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "Peeklism.ico"));
        AddShortcut(VirtualKey.O, VirtualKeyModifiers.Control, () => OnOpen(this, new RoutedEventArgs()));
        AddShortcut(VirtualKey.F11, VirtualKeyModifiers.None, ToggleFullScreen);
        AddShortcut(VirtualKey.Escape, VirtualKeyModifiers.None, ExitFullScreen);
        AddShortcut(VirtualKey.Number1, VirtualKeyModifiers.Control, () => _image?.Actual(), () => _image is not null);
        AddShortcut(VirtualKey.Number0, VirtualKeyModifiers.Control, () => _image?.Fit(), () => _image is not null);
        AddShortcut(VirtualKey.R, VirtualKeyModifiers.Control, () => OnReload(this, new RoutedEventArgs()));
        AddShortcut(VirtualKey.Left, VirtualKeyModifiers.Menu, () => Navigate(-1));
        AddShortcut(VirtualKey.Right, VirtualKeyModifiers.Menu, () => Navigate(1));
        Closed += (_, _) => { _closed = true; _generation++; ReleaseContent(); };
        UpdateCommands();
        Root.Loaded += async (_, _) =>
        {
            if (paths.Count == 0) return;
            _playlist = paths.Select(SafeFullPath).ToList();
            await OpenAsync(_playlist[0], discoverNeighbors: paths.Count == 1);
        };
    }

    private static string SafeFullPath(string path)
    {
        try { return Path.GetFullPath(path); }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException) { return path; }
    }

    private void AddShortcut(VirtualKey key, VirtualKeyModifiers modifiers, Action action, Func<bool>? enabled = null)
    {
        var shortcut = new KeyboardAccelerator { Key = key, Modifiers = modifiers };
        shortcut.Invoked += (_, e) => { if (enabled?.Invoke() == false) return; action(); e.Handled = true; };
        Root.KeyboardAccelerators.Add(shortcut);
    }

    private async Task OpenAsync(string path, bool discoverNeighbors = false)
    {
        var generation = ++_generation;
        ReleaseContent();
        _path = SafeFullPath(path);
        _kind = FileKind.Unknown;
        FileLabel.Text = _path;
        ToolTipService.SetToolTip(FileLabel, _path);
        Title = $"{Path.GetFileName(_path)} — Peeklism";
        EmptyState.Visibility = Visibility.Collapsed;
        ErrorState.Visibility = Visibility.Collapsed;
        SetLoading(true);
        StatusLabel.Text = "載入中";
        UpdateCommands();
        try
        {
            if (!File.Exists(_path)) throw new IOException("檔案不存在、無法存取，或選取的是資料夾。請確認路徑後重新開啟。");
            _kind = FileKinds.Classify(_path);
            UpdateCommands();
            if (discoverNeighbors)
            {
                var currentPath = _path;
                var kind = _kind;
                var neighbors = await Task.Run(() =>
                {
                    try
                    {
                        return Directory.EnumerateFiles(Path.GetDirectoryName(currentPath)!)
                            .Where(file => FileKinds.Classify(file) == kind)
                            .OrderBy(file => Path.GetFileName(file), StringComparer.CurrentCultureIgnoreCase).ToList();
                    }
                    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { return [currentPath]; }
                });
                if (!IsCurrent(generation)) return;
                _playlist = neighbors;
                UpdateCommands();
            }
            switch (_kind)
            {
                case FileKind.Image:
                    var image = new ImageViewer();
                    _image = image;
                    image.StatusChanged += status => { if (IsCurrent(generation)) StatusLabel.Text = status; };
                    Host.Content = image;
                    await image.LoadAsync(_path);
                    break;
                case FileKind.Video:
                case FileKind.Audio:
                    CreateMedia(_path, generation);
                    return;
                case FileKind.Markdown:
                case FileKind.Text:
                case FileKind.Pdf:
                    var document = new DocumentViewer();
                    _document = document;
                    Host.Content = document.View;
                    document.LinkRequested += uri => DispatcherQueue.TryEnqueue(async () =>
                    {
                        if (IsCurrent(generation)) await OpenLinkAsync(uri);
                    });
                    document.Failed += message => DispatcherQueue.TryEnqueue(() =>
                    {
                        if (IsCurrent(generation)) ShowError(message);
                    });
                    document.Loaded += () =>
                    {
                        if (!IsCurrent(generation)) return;
                        SetLoading(false);
                        StatusLabel.Text = _kind == FileKind.Pdf ? "完整 PDF · Ctrl+F 搜尋" : "完整文件 · Ctrl+F 搜尋";
                    };
                    await document.LoadAsync(_path, _kind == FileKind.Markdown, SourceButton.IsChecked == true,
                        Root.ActualTheme == ElementTheme.Dark);
                    // NavigationCompleted owns the loading state for browser content.
                    return;
                default:
                    throw new NotSupportedException("目前沒有這種格式的查看器。支援圖片、影片、音訊、PDF、Markdown 與文字／程式碼檔案。");
            }
            if (IsCurrent(generation)) SetLoading(false);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            if (IsCurrent(generation))
                ShowError($"{exception.Message}\n\n圖片／影音可能需要 Windows 的 HEIF、AV1 或 HEVC 解碼器；PDF／文件需要 Microsoft Edge WebView2 Runtime。");
        }
    }

    private bool IsCurrent(int generation) => !_closed && generation == _generation;

    private void CreateMedia(string path, int generation)
    {
        var player = new MediaPlayer { AutoPlay = true, IsMuted = false, Volume = .7 };
        _player = player;
        _mediaSource = MediaSource.CreateFromUri(new Uri(path));
        player.MediaFailed += (_, e) => DispatcherQueue.TryEnqueue(() =>
        {
            if (IsCurrent(generation)) ShowError($"無法播放：{e.ErrorMessage}\n請確認檔案完整，並安裝此格式所需的 Windows 解碼器。");
        });
        player.MediaOpened += (_, _) => DispatcherQueue.TryEnqueue(() =>
        {
            if (!IsCurrent(generation)) return;
            SetLoading(false);
            StatusLabel.Text = "空白鍵 播放／暫停 · 方向鍵 快轉／倒轉 5 秒";
        });
        _mediaElement = new MediaPlayerElement { AreTransportControlsEnabled = true };
        _mediaElement.SetMediaPlayer(player);
        _mediaElement.TransportControls.IsZoomButtonVisible = true;
        _mediaElement.TransportControls.IsPlaybackRateButtonVisible = true;
        _mediaElement.TransportControls.IsPlaybackRateEnabled = true;
        var loop = new ToggleSwitch { Header = "循環播放", IsOn = false };
        loop.Toggled += (_, _) => player.IsLoopingEnabled = loop.IsOn;
        var panel = new Grid();
        panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        panel.Children.Add(_mediaElement);
        loop.Margin = new Thickness(16, 8, 16, 8);
        Grid.SetRow(loop, 1);
        panel.Children.Add(loop);
        Host.Content = panel;
        player.Source = _mediaSource;
    }

    private void SetLoading(bool value)
    {
        LoadingState.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        LoadingRing.IsActive = value;
    }

    private void ShowError(string message)
    {
        ReleaseContent();
        SetLoading(false);
        ErrorText.Text = message;
        ErrorState.Visibility = Visibility.Visible;
        StatusLabel.Text = "未載入 · 可重試或選擇其他檔案";
    }

    private void ReleaseContent()
    {
        _mediaElement?.SetMediaPlayer(null);
        _mediaElement = null;
        if (_player is not null) { _player.Pause(); _player.Source = null; _player.Dispose(); _player = null; }
        _mediaSource?.Dispose();
        _mediaSource = null;
        _image?.Dispose();
        _image = null;
        _document?.Dispose();
        _document = null;
        Host.Content = null;
    }

    private void UpdateCommands()
    {
        var index = _path is null ? -1 : _playlist.FindIndex(path => path.Equals(_path, StringComparison.OrdinalIgnoreCase));
        PreviousButton.IsEnabled = index > 0;
        NextButton.IsEnabled = index >= 0 && index < _playlist.Count - 1;
        var image = _path is not null && _kind == FileKind.Image;
        FitButton.Visibility = ActualButton.Visibility = RotateButton.Visibility = image ? Visibility.Visible : Visibility.Collapsed;
        ZoomInButton.Visibility = ZoomOutButton.Visibility = image ? Visibility.Visible : Visibility.Collapsed;
        SourceButton.Visibility = _kind == FileKind.Markdown ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Navigate(int direction)
    {
        if (_path is null) return;
        var index = _playlist.FindIndex(path => path.Equals(_path, StringComparison.OrdinalIgnoreCase)) + direction;
        if (index < 0 || index >= _playlist.Count) return;
        SourceButton.IsChecked = false;
        _ = OpenAsync(_playlist[index]);
    }

    private async void OnOpen(object sender, RoutedEventArgs e)
    {
        if (_picking) return;
        _picking = true;
        try
        {
            var picker = new FileOpenPicker();
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
            picker.FileTypeFilter.Add("*");
            var files = await picker.PickMultipleFilesAsync();
            if (_closed || files.Count == 0) return;
            _playlist = files.Select(file => file.Path).ToList();
            SourceButton.IsChecked = false;
            await OpenAsync(_playlist[0], files.Count == 1);
        }
        catch (Exception exception) { if (!_closed) await ShowMessageAsync("無法選擇檔案", exception.Message); }
        finally { _picking = false; }
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "在 Peeklism 開啟（不修改原檔）";
        }
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        try
        {
            if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;
            var items = await e.DataView.GetStorageItemsAsync();
            if (_closed) return;
            _playlist = items.Where(item => item is Windows.Storage.StorageFile).Select(item => item.Path).ToList();
            if (_playlist.Count == 0) { await ShowMessageAsync("請選擇檔案", "完整查看器不開啟資料夾。請拖入圖片、影音或文件。"); return; }
            SourceButton.IsChecked = false;
            await OpenAsync(_playlist[0], _playlist.Count == 1);
        }
        catch (Exception exception) { if (!_closed) await ShowMessageAsync("無法開啟拖入的檔案", exception.Message); }
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (_player is null || e.OriginalSource is TextBox or Slider or Button or ToggleSwitch) return;
        if (e.Key == VirtualKey.Space)
        {
            if (_player.PlaybackSession.PlaybackState == MediaPlaybackState.Playing) _player.Pause(); else _player.Play();
            e.Handled = true;
        }
        else if (e.Key is VirtualKey.Left or VirtualKey.Right && _player.PlaybackSession.CanSeek)
        {
            var session = _player.PlaybackSession;
            var seconds = Math.Clamp(session.Position.TotalSeconds + (e.Key == VirtualKey.Left ? -5 : 5),
                0, Math.Max(0, session.NaturalDuration.TotalSeconds));
            session.Position = TimeSpan.FromSeconds(seconds);
            e.Handled = true;
        }
    }

    private void OnPrevious(object sender, RoutedEventArgs e) => Navigate(-1);
    private void OnNext(object sender, RoutedEventArgs e) => Navigate(1);
    private void OnFit(object sender, RoutedEventArgs e) => _image?.Fit();
    private void OnActual(object sender, RoutedEventArgs e) => _image?.Actual();
    private void OnRotate(object sender, RoutedEventArgs e) => _image?.Rotate();
    private void OnZoomIn(object sender, RoutedEventArgs e) => _image?.Zoom(1.25);
    private void OnZoomOut(object sender, RoutedEventArgs e) => _image?.Zoom(.8);
    private void OnSource(object sender, RoutedEventArgs e) { if (_path is not null) _ = OpenAsync(_path); }
    private void OnReload(object sender, RoutedEventArgs e) { if (_path is not null) _ = OpenAsync(_path); }
    private void OnFullScreen(object sender, RoutedEventArgs e) => ToggleFullScreen();
    private void ToggleFullScreen() => AppWindow.SetPresenter(AppWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen
        ? AppWindowPresenterKind.Default : AppWindowPresenterKind.FullScreen);
    private void ExitFullScreen() { if (AppWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen) AppWindow.SetPresenter(AppWindowPresenterKind.Default); }

    private async void OnReveal(object sender, RoutedEventArgs e)
    {
        if (_path is null) return;
        try
        {
            var start = new ProcessStartInfo("explorer.exe");
            start.ArgumentList.Add("/select,");
            start.ArgumentList.Add(_path);
            Process.Start(start);
        }
        catch (Exception exception) { await ShowMessageAsync("無法開啟檔案總管", exception.Message); }
    }

    private async void OnCopyPath(object sender, RoutedEventArgs e)
    {
        if (_path is null) return;
        try { var data = new DataPackage(); data.SetText(_path); Clipboard.SetContent(data); StatusLabel.Text = "已複製路徑"; }
        catch (Exception exception) { await ShowMessageAsync("無法複製路徑", exception.Message); }
    }

    private async Task OpenLinkAsync(Uri uri)
    {
        if (_closed || _dialogOpen) return;
        if (uri.Scheme is not ("http" or "https" or "mailto") || uri.Host.EndsWith(".peeklism.invalid", StringComparison.OrdinalIgnoreCase))
        {
            await ShowMessageAsync("連結未開啟", "為保護本機檔案，文件內的本機路徑、程式與特殊協定不會直接執行。請使用「開啟」選擇要看的檔案。");
            return;
        }
        _dialogOpen = true;
        try
        {
            var dialog = new ContentDialog { XamlRoot = Root.XamlRoot, Title = "開啟外部連結？", Content = uri.AbsoluteUri,
                PrimaryButtonText = "使用預設應用程式開啟", CloseButtonText = "取消", DefaultButton = ContentDialogButton.Close };
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                await Launcher.LaunchUriAsync(uri);
        }
        catch (Exception exception) { if (!_closed) StatusLabel.Text = $"無法開啟連結：{exception.Message}"; }
        finally { _dialogOpen = false; }
    }

    private async Task ShowMessageAsync(string title, string message)
    {
        if (_closed || _dialogOpen) return;
        _dialogOpen = true;
        try { await new ContentDialog { XamlRoot = Root.XamlRoot, Title = title, Content = message, CloseButtonText = "關閉" }.ShowAsync(); }
        catch (Exception exception) { Peeklism.Core.Diagnostics.PeekLog.Write($"dialog failed: {exception.Message}"); }
        finally { _dialogOpen = false; }
    }

    private async void OnHelp(object sender, RoutedEventArgs e) => await ShowMessageAsync("查看器快捷鍵",
        "Ctrl+O　開啟檔案（可多選）\nAlt+← / →　上一個／下一個檔案\nCtrl+1　圖片原始像素\nCtrl+0　圖片符合視窗／文件重設縮放\nCtrl+滑鼠滾輪　縮放\n圖片雙擊　符合視窗／原始像素切換\n拖曳圖片　平移\nCtrl+F　文件／PDF 搜尋（焦點在文件內）\nCtrl+R　重新載入\nF11　全螢幕\nEsc　離開全螢幕\n空白鍵　影音播放／暫停\n\n完整模式不會因切換視窗或按空白鍵而關閉。Markdown 的內嵌 HTML 與遠端圖片預設停用；本機圖片限於文件所在資料夾。MDX 以 Markdown 顯示，不執行元件。");
}
