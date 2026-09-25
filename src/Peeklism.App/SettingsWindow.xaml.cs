using Microsoft.UI.Xaml;
using Peeklism.Core.Settings;

namespace Peeklism.App;

public sealed partial class SettingsWindow : Window
{
    private readonly Func<bool> _getLogin;
    private readonly Action<bool> _setLogin;
    private readonly Action<PreviewSettings> _apply;
    private PreviewSettings _settings;
    private bool _loading;

    public SettingsWindow(PreviewSettings settings, Func<bool> getLogin,
        Action<bool> setLogin, Action<PreviewSettings> apply)
    {
        _settings = settings;
        _getLogin = getLogin;
        _setLogin = setLogin;
        _apply = apply;
        _loading = true;
        InitializeComponent();
        SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop();
        AppWindow.Resize(new Windows.Graphics.SizeInt32(660, 440));
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "Peeklism.ico"));
        TopmostSwitch.IsOn = settings.AlwaysOnTop;
        PreloadSwitch.IsOn = settings.PreloadPreview;
        LoginSwitch.IsOn = getLogin();
        _loading = false;
    }

    private void OnTopmostChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        Update(_settings with { AlwaysOnTop = TopmostSwitch.IsOn });
    }

    private void OnPreloadChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        Update(_settings with { PreloadPreview = PreloadSwitch.IsOn });
    }

    private void Update(PreviewSettings next)
    {
        try
        {
            next.Save();
            _settings = next;
            _apply(next);
            StatusText.Text = "設定已儲存。";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            _loading = true;
            TopmostSwitch.IsOn = _settings.AlwaysOnTop;
            PreloadSwitch.IsOn = _settings.PreloadPreview;
            _loading = false;
            StatusText.Text = "無法儲存設定。請確認使用者資料夾可寫入後重試。";
        }
    }

    private void OnLoginChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        try
        {
            _setLogin(LoginSwitch.IsOn);
            StatusText.Text = "設定已儲存。";
        }
        catch (Exception error) when (error is InvalidOperationException or UnauthorizedAccessException)
        {
            _loading = true;
            LoginSwitch.IsOn = _getLogin();
            _loading = false;
            StatusText.Text = "無法更改登入啟動設定。請檢查 Windows 的啟動應用程式權限。";
        }
    }
}
