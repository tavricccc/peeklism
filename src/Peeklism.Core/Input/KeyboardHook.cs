using System.Diagnostics;
using System.Runtime.InteropServices;
using Peeklism.Core.Diagnostics;

namespace Peeklism.Core.Input;

public sealed class KeyInterceptedEventArgs(int virtualKey, bool hasModifiers) : EventArgs
{
    public int VirtualKey { get; } = virtualKey;

    /// <summary>Whether Ctrl, Alt, Shift or Win was down. Those combinations belong to the app.</summary>
    public bool HasModifiers { get; } = hasModifiers;

    /// <summary>Set to swallow the key so the focused application never sees it.</summary>
    public bool Handled { get; set; }
}

/// <summary>
/// A low-level keyboard hook, installed only while it can actually be needed.
/// </summary>
/// <remarks>
/// Windows drops a hook whose callback is slow, which would stall every keystroke on the
/// machine, so the callback does nothing but raise an event the caller answers from a
/// handful of cached comparisons. Installing on demand also keeps Peeklism from holding a
/// system-wide keyboard hook while the user is working in an unrelated application.
/// </remarks>
public sealed class KeyboardHook : IDisposable
{
    private const int HookTypeLowLevelKeyboard = 13;
    private const int HookActionKeyDown = 0x0100;
    private const int HookActionSystemKeyDown = 0x0104;
    private const int VirtualKeyShift = 0x10;
    private const int VirtualKeyControl = 0x11;
    private const int VirtualKeyAlt = 0x12;
    private const int VirtualKeyLeftWindows = 0x5B;
    private const int VirtualKeyRightWindows = 0x5C;

    private readonly NativeMethods.HookProcedure _callback;
    private nint _hook;
    private bool _disposed;

    public KeyboardHook() => _callback = ProcessKey;

    public event EventHandler<KeyInterceptedEventArgs>? KeyIntercepted;

    public bool IsInstalled => _hook != 0;

    public void Install()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_hook != 0)
        {
            return;
        }

        // A low-level hook is not injected, so it needs no module handle of its own.
        _hook = NativeMethods.SetWindowsHookExW(HookTypeLowLevelKeyboard, _callback, 0, 0);
        if (_hook == 0)
        {
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }

        PeekLog.Write("keyboard hook installed");
    }

    public void Uninstall()
    {
        if (_hook == 0)
        {
            return;
        }

        NativeMethods.UnhookWindowsHookEx(_hook);
        _hook = 0;
        PeekLog.Write("keyboard hook removed");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Uninstall();
    }

    private nint ProcessKey(int code, nint messageType, nint data)
    {
        if (code < 0 || KeyIntercepted is null)
        {
            return NativeMethods.CallNextHookEx(0, code, messageType, data);
        }

        var action = (int)messageType;
        if (action is not (HookActionKeyDown or HookActionSystemKeyDown))
        {
            return NativeMethods.CallNextHookEx(0, code, messageType, data);
        }

        try
        {
            var input = Marshal.PtrToStructure<NativeMethods.KeyboardInput>(data);
            var args = new KeyInterceptedEventArgs((int)input.VirtualKey, AnyModifierDown());
            KeyIntercepted.Invoke(this, args);
            if (args.Handled)
            {
                return 1;
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // Never let a fault in our own handling break typing system-wide.
            Debug.WriteLine($"Keyboard hook handling failed, passing the key through: {exception}");
        }

        return NativeMethods.CallNextHookEx(0, code, messageType, data);
    }

    private static bool AnyModifierDown() =>
        IsDown(VirtualKeyShift) || IsDown(VirtualKeyControl) || IsDown(VirtualKeyAlt)
        || IsDown(VirtualKeyLeftWindows) || IsDown(VirtualKeyRightWindows);

    private static bool IsDown(int virtualKey) => (NativeMethods.GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    private static class NativeMethods
    {
        internal delegate nint HookProcedure(int code, nint messageType, nint data);

        [StructLayout(LayoutKind.Sequential)]
        internal struct KeyboardInput
        {
            public uint VirtualKey;
            public uint ScanCode;
            public uint Flags;
            public uint Time;
            public nuint ExtraInfo;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern nint SetWindowsHookExW(int hookType, HookProcedure callback, nint module, uint threadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UnhookWindowsHookEx(nint hook);

        [DllImport("user32.dll")]
        internal static extern nint CallNextHookEx(nint hook, int code, nint messageType, nint data);

        [DllImport("user32.dll")]
        internal static extern short GetAsyncKeyState(int virtualKey);
    }
}
