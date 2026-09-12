using System.Runtime.InteropServices;
using System.Text;

namespace Peeklism.Core.Shell;

/// <summary>
/// Classifies the real foreground window. Every call here is a plain user32 lookup so it
/// stays fast enough to run inside the low-level keyboard hook's callback.
/// </summary>
public static class NativeForegroundWindowClassifier
{
    public static nint GetForegroundWindow() => NativeMethods.GetForegroundWindow();

    public static FocusedWindowType Classify(nint windowHandle) =>
        ForegroundWindowClassifier.Classify(windowHandle, GetClassName, HasDescendantOfClass);

    public static FocusedWindowType ClassifyForeground() => Classify(GetForegroundWindow());

    private static string GetClassName(nint windowHandle)
    {
        var buffer = new StringBuilder(256);
        var length = NativeMethods.GetClassNameW(windowHandle, buffer, buffer.Capacity);
        return length == 0 ? string.Empty : buffer.ToString(0, length);
    }

    private static bool HasDescendantOfClass(nint windowHandle, string className)
    {
        var found = false;
        NativeMethods.EnumChildWindows(windowHandle, (child, _) =>
        {
            if (!GetClassName(child).Equals(className, StringComparison.Ordinal))
            {
                return true;
            }

            found = true;
            return false;
        }, 0);
        return found;
    }

    private static class NativeMethods
    {
        internal delegate bool EnumWindowsProc(nint windowHandle, nint parameter);

        [DllImport("user32.dll")]
        internal static extern nint GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern int GetClassNameW(nint windowHandle, StringBuilder buffer, int bufferSize);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool EnumChildWindows(nint parent, EnumWindowsProc callback, nint parameter);
    }
}
