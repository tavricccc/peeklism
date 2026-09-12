using System.Diagnostics;

namespace Peeklism.Core.Shell;

public interface IShellSelectionProvider
{
    /// <summary>
    /// Reads the current selection of the given foreground window. Never throws: a surface
    /// that cannot be read must fall back to passing the key press through, not to an error.
    /// </summary>
    ShellSelection GetSelection(FocusedWindowType windowType, nint windowHandle);
}

/// <summary>
/// Reads the selection out of File Explorer and the desktop through shell automation.
/// </summary>
/// <remarks>
/// <see cref="FocusedWindowType.Dialog"/> returns <see cref="ShellSelection.Empty"/> here.
/// A common file dialog belongs to another process and is not registered with the shell
/// window collection, so reading it needs in-process code there — the job of the native
/// helper, which loads into the dialog's own thread.
/// </remarks>
public sealed class ShellSelectionProvider(StaExecutor executor) : IShellSelectionProvider
{
    public ShellSelection GetSelection(FocusedWindowType windowType, nint windowHandle)
    {
        if (windowType is FocusedWindowType.Invalid or FocusedWindowType.Dialog)
        {
            return ShellSelection.Empty;
        }

        try
        {
            return executor.Invoke(() => Read(windowType, windowHandle));
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            Debug.WriteLine($"Reading the shell selection failed: {exception}");
            return ShellSelection.Empty;
        }
    }

    private static ShellSelection Read(FocusedWindowType windowType, nint windowHandle)
    {
        var windows = ShellWindowsInterop.Create();
        if (windows is null)
        {
            return ShellSelection.Empty;
        }

        try
        {
            var window = windowType == FocusedWindowType.Desktop
                ? FindDesktop(windows)
                : FindExplorer(windows, windowHandle);
            return window is null ? ShellSelection.Empty : ReadSelection(window);
        }
        finally
        {
            Release(windows);
        }
    }

    private static object? FindDesktop(IShellWindows windows)
    {
        // CSIDL_DESKTOP, expressed the way FindWindowSW expects its VARIANT arguments.
        object location = 0;
        object root = 0;
        return windows.FindWindowSW(
            ref location,
            ref root,
            ShellWindowsInterop.ShellWindowClassDesktop,
            out _,
            ShellWindowsInterop.FindWindowNeedDispatch);
    }

    private static object? FindExplorer(IShellWindows windows, nint windowHandle)
    {
        var count = windows.Count;
        for (var index = 0; index < count; index++)
        {
            var candidate = windows.Item(index);
            if (candidate is null)
            {
                continue;
            }

            if (MatchesWindowHandle(candidate, windowHandle))
            {
                return candidate;
            }

            Release(candidate);
        }

        return null;
    }

    private static bool MatchesWindowHandle(object shellWindow, nint windowHandle)
    {
        try
        {
            // Late binding: the collection hands back IWebBrowser2 objects whose interop
            // assembly Peeklism deliberately does not take a dependency on.
            dynamic browser = shellWindow;
            return (nint)(long)browser.HWND == windowHandle;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return false;
        }
    }

    private static ShellSelection ReadSelection(object shellWindow)
    {
        try
        {
            dynamic browser = shellWindow;
            dynamic items = browser.Document.SelectedItems();
            int count = items.Count;
            if (count == 0)
            {
                return ShellSelection.Empty;
            }

            string? path = items.Item(0).Path;
            // Virtual items such as This PC report a parsing name rather than a real path.
            if (!string.IsNullOrEmpty(path) && path.StartsWith("::{", StringComparison.Ordinal))
            {
                path = null;
            }

            return new ShellSelection(path, count);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            Debug.WriteLine($"Reading the selected item failed: {exception}");
            return ShellSelection.Empty;
        }
        finally
        {
            Release(shellWindow);
        }
    }

    private static void Release(object comObject)
    {
        if (System.Runtime.InteropServices.Marshal.IsComObject(comObject))
        {
            System.Runtime.InteropServices.Marshal.ReleaseComObject(comObject);
        }
    }
}
