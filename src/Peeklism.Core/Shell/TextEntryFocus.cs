using System.Runtime.InteropServices;
using System.Text;

namespace Peeklism.Core.Shell;

/// <summary>
/// Detects that the shell's own keyboard focus sits in a text field.
/// </summary>
/// <remarks>
/// Renaming a file, or typing in the address or search box, all happen inside a window
/// that is still classified as File Explorer. Swallowing the space bar there would corrupt
/// what the user is typing, which is the single worst failure this feature can have.
/// QuickLook guards against it by ignoring the space bar for a second after any other key;
/// asking the shell where its focus actually is avoids both the guesswork and the delay.
/// </remarks>
public static class TextEntryFocus
{
    private static readonly string[] TextEntryClassNames =
    [
        "Edit",
        "RichEdit",
        "RichEdit20W",
        "RICHEDIT50W",
        "SearchEditBox",
        "ComboBox",
    ];

    public static bool IsFocused(nint windowHandle)
    {
        if (windowHandle == 0)
        {
            return false;
        }

        var threadId = NativeMethods.GetWindowThreadProcessId(windowHandle, 0);
        if (threadId == 0)
        {
            return false;
        }

        var info = new NativeMethods.GuiThreadInfo
        {
            Size = (uint)Marshal.SizeOf<NativeMethods.GuiThreadInfo>(),
        };
        if (!NativeMethods.GetGUIThreadInfo(threadId, ref info) || info.FocusWindow == 0)
        {
            return false;
        }

        var className = GetClassName(info.FocusWindow);
        return Array.Exists(
            TextEntryClassNames,
            candidate => className.Equals(candidate, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetClassName(nint windowHandle)
    {
        var buffer = new StringBuilder(256);
        var length = NativeMethods.GetClassNameW(windowHandle, buffer, buffer.Capacity);
        return length == 0 ? string.Empty : buffer.ToString(0, length);
    }

    private static class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct GuiThreadInfo
        {
            public uint Size;
            public uint Flags;
            public nint ActiveWindow;
            public nint FocusWindow;
            public nint CaptureWindow;
            public nint MenuOwnerWindow;
            public nint MoveSizeWindow;
            public nint CaretWindow;
            public int CaretLeft;
            public int CaretTop;
            public int CaretRight;
            public int CaretBottom;
        }

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern uint GetWindowThreadProcessId(nint windowHandle, nint processId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetGUIThreadInfo(uint threadId, ref GuiThreadInfo info);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern int GetClassNameW(nint windowHandle, StringBuilder buffer, int bufferSize);
    }
}
