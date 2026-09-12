using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Peeklism.App.Services;
using Peeklism.Core.Diagnostics;
using Peeklism.Core.Lifecycle;
using Peeklism.Core.Shell;

namespace Peeklism.App;

public partial class App : Application
{
    private SingleInstanceGate? _instanceGate;
    private AppShutdownSignal? _shutdownSignal;
    private StaExecutor? _shellExecutor;
    private PreviewWindow? _window;
    private PreviewController? _controller;
    private TrayIcon? _tray;
    private bool _isExiting;

    public App() => InitializeComponent();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
#if DEBUG
        PeekLog.IsEnabled = true;
#endif
        PeekLog.Write("---- Peeklism starting ----");

        // Two instances would mean two keyboard hooks fighting over the same space bar.
        _instanceGate = new SingleInstanceGate("Peeklism.App", () => { });
        if (!_instanceGate.IsPrimary)
        {
            PeekLog.Write("another instance is already running; exiting");
            _instanceGate.Dispose();
            Exit();
            return;
        }

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
        _tray = new TrayIcon();
        _tray.PauseToggled += OnPauseToggled;
        _tray.AboutRequested += OnAboutRequested;
        _tray.ExitRequested += (_, _) => ExitApplication();

        // The installer asks a running copy to quit before it replaces the files.
        _shutdownSignal = new AppShutdownSignal(
            Environment.ProcessId,
            () => _window.DispatcherQueue.TryEnqueue(ExitApplication));
        PeekLog.Write($"ready; log at {PeekLog.FilePath}");
    }

    private void OnPauseToggled(object? sender, EventArgs args)
    {
        if (_controller is null || _tray is null)
        {
            return;
        }

        var paused = _controller.IsEnabled;
        _controller.SetEnabled(!paused);
        _tray.IsPaused = paused;
        _tray.UpdateTooltip(paused ? "Peeklism（已暫停）" : "Peeklism");
        PeekLog.Write($"previewing {(paused ? "paused" : "resumed")}");
    }

    private void OnAboutRequested(object? sender, EventArgs args)
    {
        var version = typeof(App).Assembly.GetName().Version?.ToString(3) ?? "0.1.0";
        MessageBoxW(
            0,
            $"Peeklism {version}\n\n在檔案總管或桌面選取檔案後按空白鍵即可預覽。\n"
                + "再按一次空白鍵或 Esc 關閉。",
            "關於 Peeklism",
            0x40);
    }

    private void ExitApplication()
    {
        if (_isExiting)
        {
            return;
        }

        _isExiting = true;
        PeekLog.Write("exiting");
        _tray?.Dispose();
        _controller?.Dispose();
        _shutdownSignal?.Dispose();
        _window?.Close();
        _shellExecutor?.Dispose();
        _instanceGate?.Dispose();
        Exit();
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(nint window, string text, string caption, uint type);
}
