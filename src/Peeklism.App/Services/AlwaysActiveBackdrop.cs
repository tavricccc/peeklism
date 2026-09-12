using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using WinRT;

namespace Peeklism.App.Services;

/// <summary>
/// Applies Mica and keeps it in its active state.
/// </summary>
/// <remarks>
/// The XAML <c>MicaBackdrop</c> follows real window activation, and DWM paints the flat
/// inactive fallback for anything that is not the foreground window. Peeklism's preview
/// never takes focus by design, so it would always get that fallback. Driving the
/// controller directly lets the backdrop stay active while the shell keeps the keyboard.
/// </remarks>
public sealed class AlwaysActiveBackdrop : IDisposable
{
    private MicaController? _controller;
    private SystemBackdropConfiguration? _configuration;

    public bool TryApply(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (!MicaController.IsSupported())
        {
            return false;
        }

        _configuration = new SystemBackdropConfiguration
        {
            IsInputActive = true,
            Theme = ResolveTheme(window),
        };

        _controller = new MicaController { Kind = MicaKind.Base };
        _controller.SetSystemBackdropConfiguration(_configuration);
        _controller.AddSystemBackdropTarget(window.As<ICompositionSupportsSystemBackdrop>());

        if (window.Content is FrameworkElement root)
        {
            root.ActualThemeChanged += (sender, _) =>
            {
                if (_configuration is not null)
                {
                    _configuration.Theme = ToBackdropTheme(sender.ActualTheme);
                }
            };
        }

        return true;
    }

    public void Dispose()
    {
        _controller?.Dispose();
        _controller = null;
        _configuration = null;
    }

    private static SystemBackdropTheme ResolveTheme(Window window) =>
        window.Content is FrameworkElement root
            ? ToBackdropTheme(root.ActualTheme)
            : SystemBackdropTheme.Default;

    private static SystemBackdropTheme ToBackdropTheme(ElementTheme theme) => theme switch
    {
        ElementTheme.Light => SystemBackdropTheme.Light,
        ElementTheme.Dark => SystemBackdropTheme.Dark,
        _ => SystemBackdropTheme.Default,
    };
}
