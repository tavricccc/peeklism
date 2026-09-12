using Microsoft.UI.Xaml;
using Peeklism.App.Services;
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
        _shellExecutor = new StaExecutor("Peeklism.Shell.Sta");
        _window = new PreviewWindow();

        // Paid once at start-up, in the tray, rather than on the first space bar press.
        _window.WarmUp();
        _controller = new PreviewController(_window, new ShellSelectionProvider(_shellExecutor));
    }
}
