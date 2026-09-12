using System.Runtime.InteropServices;
using Peeklism.Core.Diagnostics;
using Peeklism.Core.Shell;

namespace Peeklism.Core.Input;

public sealed class ShellSurfaceChangedEventArgs(FocusedWindowType windowType, nint windowHandle) : EventArgs
{
    public FocusedWindowType WindowType { get; } = windowType;

    public nint WindowHandle { get; } = windowHandle;
}

/// <summary>
/// Reports which shell surface is in front, so the keyboard hook can be installed only
/// while the space bar could plausibly belong to Peeklism.
/// </summary>
/// <remarks>
/// Requires a running message loop on the thread that constructs it.
/// </remarks>
public sealed class ForegroundWatcher : IDisposable
{
    private const uint EventSystemForeground = 0x0003;
    private const uint WinEventOutOfContext = 0x0000;
    private const uint WinEventSkipOwnProcess = 0x0002;

    private readonly NativeMethods.WinEventProcedure _callback;
    private nint _hook;

    public ForegroundWatcher()
    {
        _callback = OnForegroundChanged;
        _hook = NativeMethods.SetWinEventHook(
            EventSystemForeground,
            EventSystemForeground,
            0,
            _callback,
            0,
            0,
            WinEventOutOfContext | WinEventSkipOwnProcess);
    }

    public event EventHandler<ShellSurfaceChangedEventArgs>? ShellSurfaceChanged;

    public FocusedWindowType CurrentWindowType { get; private set; } = FocusedWindowType.Invalid;

    public nint CurrentWindowHandle { get; private set; }

    /// <summary>Classifies whatever is in front right now, without waiting for a change.</summary>
    public void Refresh()
    {
        var handle = NativeForegroundWindowClassifier.GetForegroundWindow();
        Publish(handle, NativeForegroundWindowClassifier.Classify(handle));
    }

    public void Dispose()
    {
        if (_hook == 0)
        {
            return;
        }

        NativeMethods.UnhookWinEvent(_hook);
        _hook = 0;
    }

    private void OnForegroundChanged(
        nint hook,
        uint eventId,
        nint windowHandle,
        int objectId,
        int childId,
        uint threadId,
        uint timestamp)
    {
        // Only the window-level event carries a foreground change; object and caret events
        // for the same window arrive here too and would classify as nothing useful.
        if (objectId != 0 || windowHandle == 0)
        {
            return;
        }

        Publish(windowHandle, NativeForegroundWindowClassifier.Classify(windowHandle));
    }

    private void Publish(nint windowHandle, FocusedWindowType windowType)
    {
        if (windowHandle == CurrentWindowHandle && windowType == CurrentWindowType)
        {
            return;
        }

        CurrentWindowHandle = windowHandle;
        CurrentWindowType = windowType;
        PeekLog.Write($"foreground: {windowType} hwnd=0x{windowHandle:X}");
        ShellSurfaceChanged?.Invoke(this, new ShellSurfaceChangedEventArgs(windowType, windowHandle));
    }

    private static class NativeMethods
    {
        internal delegate void WinEventProcedure(
            nint hook,
            uint eventId,
            nint windowHandle,
            int objectId,
            int childId,
            uint threadId,
            uint timestamp);

        [DllImport("user32.dll")]
        internal static extern nint SetWinEventHook(
            uint eventMin,
            uint eventMax,
            nint module,
            WinEventProcedure callback,
            uint processId,
            uint threadId,
            uint flags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UnhookWinEvent(nint hook);
    }
}
