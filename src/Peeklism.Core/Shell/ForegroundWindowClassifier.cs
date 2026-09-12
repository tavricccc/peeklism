namespace Peeklism.Core.Shell;

/// <summary>
/// Decides which shell surface a window belongs to from its class name alone.
/// </summary>
/// <remarks>
/// The classification is kept free of P/Invoke so it can be exercised directly by tests;
/// <see cref="NativeForegroundWindowClassifier"/> supplies the real window lookups.
/// </remarks>
public static class ForegroundWindowClassifier
{
    /// <param name="getClassName">Returns the window class name, or an empty string.</param>
    /// <param name="hasDescendantOfClass">Whether the window has a descendant of that class.</param>
    public static FocusedWindowType Classify(
        nint windowHandle,
        Func<nint, string> getClassName,
        Func<nint, string, bool> hasDescendantOfClass)
    {
        ArgumentNullException.ThrowIfNull(getClassName);
        ArgumentNullException.ThrowIfNull(hasDescendantOfClass);
        if (windowHandle == 0)
        {
            return FocusedWindowType.Invalid;
        }

        var className = getClassName(windowHandle);
        if (string.IsNullOrEmpty(className))
        {
            return FocusedWindowType.Invalid;
        }

        if (Matches(className, WindowClassNames.ExplorerCabinet)
            || Matches(className, WindowClassNames.ExplorerLegacy))
        {
            return FocusedWindowType.Explorer;
        }

        // Which of the two the desktop currently uses depends on whether a wallpaper slideshow
        // or third-party shell is running, so both are accepted and confirmed by the shell view.
        if (Matches(className, WindowClassNames.DesktopProgman)
            || Matches(className, WindowClassNames.DesktopWorkerW))
        {
            return hasDescendantOfClass(windowHandle, WindowClassNames.ShellDefView)
                ? FocusedWindowType.Desktop
                : FocusedWindowType.Invalid;
        }

        if (Matches(className, WindowClassNames.Dialog))
        {
            // A message box is also a #32770. Only a dialog hosting a shell view can have a
            // file selection, and swallowing the space bar on a message box would be a bug.
            return hasDescendantOfClass(windowHandle, WindowClassNames.DialogDirectUiView)
                || hasDescendantOfClass(windowHandle, WindowClassNames.ShellDefView)
                ? FocusedWindowType.Dialog
                : FocusedWindowType.Invalid;
        }

        return FocusedWindowType.Invalid;
    }

    private static bool Matches(string className, string expected) =>
        className.Equals(expected, StringComparison.Ordinal);
}
