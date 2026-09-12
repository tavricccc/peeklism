using Microsoft.UI.Dispatching;
using Peeklism.Core.Input;
using Peeklism.Core.Shell;

namespace Peeklism.App.Services;

/// <summary>
/// Turns a space bar press over a shell surface into a preview, and keeps that preview in
/// step with whatever the user selects next.
/// </summary>
public sealed class PreviewController : IDisposable
{
    private const int VirtualKeySpace = 0x20;
    private const int VirtualKeyEscape = 0x1B;
    private const int VirtualKeyReturn = 0x0D;

    /// <summary>
    /// How often the selection is re-read while a preview is up. Polling covers arrow keys,
    /// mouse clicks and selections changed by anything else, which no single key handler can.
    /// </summary>
    private static readonly TimeSpan SelectionPollInterval = TimeSpan.FromMilliseconds(150);

    private readonly PreviewWindow _window;
    private readonly IShellSelectionProvider _selection;
    private readonly DispatcherQueue _dispatcher;
    private readonly ForegroundWatcher _foreground = new();
    private readonly KeyboardHook _keyboard = new();
    private readonly DispatcherQueueTimer _selectionPoll;
    private bool _disposed;

    public PreviewController(PreviewWindow window, IShellSelectionProvider selection)
    {
        _window = window;
        _selection = selection;
        _dispatcher = window.DispatcherQueue;
        _selectionPoll = _dispatcher.CreateTimer();
        _selectionPoll.Interval = SelectionPollInterval;
        _selectionPoll.Tick += (_, _) => RefreshSelection();

        _keyboard.KeyIntercepted += OnKeyIntercepted;
        _foreground.ShellSurfaceChanged += OnShellSurfaceChanged;
        _foreground.Refresh();
        SyncHookState();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _selectionPoll.Stop();
        _keyboard.KeyIntercepted -= OnKeyIntercepted;
        _foreground.ShellSurfaceChanged -= OnShellSurfaceChanged;
        _keyboard.Dispose();
        _foreground.Dispose();
    }

    private void OnShellSurfaceChanged(object? sender, ShellSurfaceChangedEventArgs args)
    {
        SyncHookState();
        if (args.WindowType == FocusedWindowType.Invalid)
        {
            // The user moved to an unrelated application; a preview left floating over it
            // would be in the way.
            _dispatcher.TryEnqueue(Hide);
        }
    }

    /// <summary>
    /// Holds the system-wide keyboard hook only while a shell surface is in front. Keeping
    /// it installed permanently would put Peeklism in the path of every keystroke on the
    /// machine for no benefit.
    /// </summary>
    private void SyncHookState()
    {
        var wanted = _foreground.CurrentWindowType != FocusedWindowType.Invalid;
        try
        {
            if (wanted)
            {
                _keyboard.Install();
            }
            else
            {
                _keyboard.Uninstall();
            }
        }
        catch (System.ComponentModel.Win32Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Keyboard hook could not be installed: {exception}");
        }
    }

    /// <remarks>
    /// This runs inside the low-level hook callback, where Windows measures how long we
    /// take and unhooks us if we are slow. So the decision is made from cheap window
    /// lookups only, and the selection — a COM call — is read afterwards, off the callback.
    /// </remarks>
    private void OnKeyIntercepted(object? sender, KeyInterceptedEventArgs args)
    {
        if (args.HasModifiers || _foreground.CurrentWindowType == FocusedWindowType.Invalid)
        {
            return;
        }

        switch (args.VirtualKey)
        {
            case VirtualKeySpace:
                if (_window.IsShowing)
                {
                    args.Handled = true;
                    _dispatcher.TryEnqueue(Hide);
                    return;
                }

                // Renaming a file, or typing in the search or address box, all happen inside
                // a window still classified as Explorer. Swallowing the space there would
                // corrupt what the user is typing.
                if (TextEntryFocus.IsFocused(_foreground.CurrentWindowHandle))
                {
                    return;
                }

                args.Handled = true;
                _dispatcher.TryEnqueue(Show);
                return;

            case VirtualKeyEscape when _window.IsShowing:
                args.Handled = true;
                _dispatcher.TryEnqueue(Hide);
                return;

            case VirtualKeyReturn when _window.IsShowing:
                // Let Explorer open the item; the preview has served its purpose.
                _dispatcher.TryEnqueue(Hide);
                return;
        }
    }

    private void Show()
    {
        var windowType = _foreground.CurrentWindowType;
        var windowHandle = _foreground.CurrentWindowHandle;
        var selection = _selection.GetSelection(windowType, windowHandle);
        if (!selection.HasPath)
        {
            return;
        }

        _window.ShowFor(selection.Path!, windowHandle);
        _selectionPoll.Start();
    }

    private void Hide()
    {
        _selectionPoll.Stop();
        _window.HidePreview();
    }

    private void RefreshSelection()
    {
        if (!_window.IsShowing)
        {
            _selectionPoll.Stop();
            return;
        }

        var selection = _selection.GetSelection(
            _foreground.CurrentWindowType,
            _foreground.CurrentWindowHandle);
        if (!selection.HasPath || selection.Path == _window.CurrentPath)
        {
            return;
        }

        // Swap the content but leave the window where it is: a preview that jumps and
        // resizes under the cursor while arrowing through a folder is unusable.
        _window.UpdateContent(selection.Path!);
    }
}
