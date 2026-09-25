using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Dispatching;
using Peeklism.App.Services;
using Peeklism.Core.Diagnostics;
using Peeklism.Core.Lifecycle;
using Peeklism.Core.Shell;
using Peeklism.Core.Settings;
using Peeklism.Core.Viewing;

namespace Peeklism.App;

public partial class App : Application
{
    private SingleInstanceGate? _instanceGate;
    private AppShutdownSignal? _shutdownSignal;
    private StaExecutor? _shellExecutor;
    private PreviewWindow? _window;
    private ViewerWindow? _viewer;
    private PreviewController? _controller;
    private TrayIcon? _tray;
    private SettingsWindow? _settingsWindow;
    private readonly LoginStartupService _loginStartup = new();
    private PreviewSettings _settings = PreviewSettings.Load();
    private bool _isExiting;
    private bool _openSettingsWhenReady;

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) => PeekLog.Write($"unhandled: {e.Message}\n{e.Exception}");
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
#if DEBUG
        PeekLog.IsEnabled = true;
#endif
        PeekLog.Write("---- Peeklism starting ----");

        var request = ViewerRequest.Parse(Environment.GetCommandLineArgs().Skip(1));
        if (request.IsViewer)
        {
            // Viewer processes are independent, so Open with still works while the tray is running.
            // Only the background process owns the gate and keyboard hook.
            _viewer = new ViewerWindow(request.Paths);
            _viewer.Closed += (_, _) => { _shutdownSignal?.Dispose(); Exit(); };
            _shutdownSignal = new AppShutdownSignal(Environment.ProcessId,
                () => _viewer.DispatcherQueue.TryEnqueue(() => _viewer.Close()));
            _viewer.Activate();
            return;
        }

        // Two instances would mean two keyboard hooks fighting over the same space bar.
        var dispatcher = DispatcherQueue.GetForCurrentThread();
        _instanceGate = new SingleInstanceGate("Peeklism.App", () => dispatcher.TryEnqueue(() =>
        {
            if (_tray is null) _openSettingsWhenReady = true;
            else OpenSettings();
        }));
        if (!_instanceGate.IsPrimary)
        {
            PeekLog.Write("another instance is already running; exiting");
            _instanceGate.Dispose();
            Exit();
            return;
        }

        _shellExecutor = new StaExecutor("Peeklism.Shell.Sta");
        if (_settings.PreloadPreview) GetPreviewWindow().WarmUp();

        // Isolated look check: one preview, with no keyboard hook and no foreground watcher,
        // so the window can be worked on without File Explorer and without the controller
        // hiding it the moment focus moves elsewhere.
        if (Environment.GetEnvironmentVariable("PEEKLISM_PREVIEW") is { Length: > 0 } preview)
        {
            GetPreviewWindow().ShowFor(preview, 0);
            PeekLog.Write($"look check: {preview}");
            return;
        }

        _controller = new PreviewController(GetPreviewWindow, new ShellSelectionProvider(_shellExecutor));
        _tray = new TrayIcon { LaunchesAtLogin = _loginStartup.IsEnabled() };
        _tray.PauseToggled += OnPauseToggled;
        _tray.LaunchAtLoginToggled += OnLaunchAtLoginToggled;
        _tray.AboutRequested += OnAboutRequested;
        _tray.SettingsRequested += (_, _) => OpenSettings();
        _tray.ViewerRequested += (_, _) =>
        {
            if (!ViewerLauncher.Open()) MessageBoxW(0, "無法啟動查看器，請重新啟動 Peeklism。", "Peeklism", 0x10);
        };
        _tray.ExitRequested += (_, _) => ExitApplication();

        // The installer asks a running copy to quit before it replaces the files.
        _shutdownSignal = new AppShutdownSignal(
            Environment.ProcessId,
            () => dispatcher.TryEnqueue(ExitApplication));
        PeekLog.Write($"ready; log at {PeekLog.FilePath}");
        if (request.OpenSettings || _openSettingsWhenReady) OpenSettings();
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

    private void OnLaunchAtLoginToggled(object? sender, EventArgs args)
    {
        if (_tray is null)
        {
            return;
        }

        try
        {
            var enable = !_tray.LaunchesAtLogin;
            SetLaunchAtLogin(enable);
            PeekLog.Write($"launch at login {(enable ? "enabled" : "disabled")}");
        }
        catch (Exception exception) when (exception is InvalidOperationException or UnauthorizedAccessException)
        {
            // The tick simply stays where it was; there is no window here to report into.
            PeekLog.Write($"launch at login failed: {exception.Message}");
            _tray.LaunchesAtLogin = _loginStartup.IsEnabled();
        }
    }

    private PreviewWindow GetPreviewWindow()
    {
        if (_window is null)
        {
            _window = new PreviewWindow();
            _window.SetAlwaysOnTop(_settings.AlwaysOnTop);
        }
        return _window;
    }

    private void SetLaunchAtLogin(bool enabled)
    {
        _loginStartup.SetEnabled(enabled);
        if (_tray is not null) _tray.LaunchesAtLogin = enabled;
    }

    private void OpenSettings()
    {
        if (_settingsWindow is not null) { _settingsWindow.Activate(); return; }
        var window = new SettingsWindow(_settings, _loginStartup.IsEnabled, SetLaunchAtLogin,
            settings => { _settings = settings; _window?.SetAlwaysOnTop(settings.AlwaysOnTop); });
        _settingsWindow = window;
        window.Closed += (_, _) => _settingsWindow = null;
        window.Activate();
    }

    private void OnAboutRequested(object? sender, EventArgs args)
    {
        var version = typeof(App).Assembly.GetName().Version?.ToString(3) ?? "0.1.0";
        MessageBoxW(
            0,
            $"Peeklism {version}\n\n在檔案總管或桌面選取檔案後按空白鍵即可預覽。\n"
                + "再按一次空白鍵或 Esc 關閉。\n\n使用「開啟檔案 → Peeklism」或預覽中的「完整查看」\n可開啟原始解析度圖片、影音與完整文件。",
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
        _settingsWindow?.Close();
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
