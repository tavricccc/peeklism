namespace Peeklism.Core.Shell;

/// <summary>
/// Window class names the shell has used for these surfaces since Windows 7. They are the
/// only cheap signal available inside a low-level keyboard hook, where anything slower than
/// a few string comparisons risks the hook being dropped by Windows.
/// </summary>
public static class WindowClassNames
{
    public const string ExplorerCabinet = "CabinetWClass";
    public const string ExplorerLegacy = "ExploreWClass";
    public const string DesktopProgman = "Progman";
    public const string DesktopWorkerW = "WorkerW";
    public const string Dialog = "#32770";

    /// <summary>Hosts the shell view inside both the desktop and a common file dialog.</summary>
    public const string ShellDefView = "SHELLDLL_DefView";

    /// <summary>
    /// The DirectUI host a modern common file dialog carries. Plain message boxes share the
    /// <see cref="Dialog"/> class but have no shell view, so this distinguishes them.
    /// </summary>
    public const string DialogDirectUiView = "DUIViewWndClassName";
}
