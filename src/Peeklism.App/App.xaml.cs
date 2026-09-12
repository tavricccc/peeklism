using Microsoft.UI.Xaml;
using Peeklism.App.Services;
using Peeklism.Core.Diagnostics;
using Peeklism.Core.Shell;

namespace Peeklism.App;

public partial class App : Application
{
    private StaExecutor? _shellExecutor;
    private PreviewWindow? _window;
    private PreviewController? _controller;

    public App() => InitializeComponent();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
#if DEBUG
        PeekLog.IsEnabled = true;
#endif
        PeekLog.Write("---- Peeklism starting ----");
        _shellExecutor = new StaExecutor("Peeklism.Shell.Sta");
        _window = new PreviewWindow();

        // Paid once at start-up, in the tray, rather than on the first space bar press.
        _window.WarmUp();

        // Isolated look check: one preview, with no keyboard hook and no foreground watcher,
        // so the window can be worked on without File Explorer and without the controller
        // hiding it the moment focus moves elsewhere.
        if (Environment.GetEnvironmentVariable("PEEKLISM_PREVIEW") is { Length: > 0 } preview)
        {
            _window.ShowFor(preview, 0);
            PeekLog.Write($"look check: {preview}");
            return;
        }

        _controller = new PreviewController(_window, new ShellSelectionProvider(_shellExecutor));
        PeekLog.Write($"ready; log at {PeekLog.FilePath}");
    }
}
