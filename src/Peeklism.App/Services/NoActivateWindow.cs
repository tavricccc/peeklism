using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace Peeklism.App.Services;

/// <summary>
/// Keeps the preview window off the activation chain.
/// </summary>
/// <remarks>
/// This is the decision the whole feel of Peeklism rests on. A preview that steals focus
/// has to forward the arrow keys back to File Explorer to keep the selection moving, which
/// drops keys and interrupts an in-progress rename. With WS_EX_NOACTIVATE the shell simply
/// never loses the keyboard, and the preview only follows what the user selects.
/// </remarks>
public sealed class NoActivateWindow
{
    private const int ExtendedStyleIndex = -20;
    private const long AppWindowExtendedStyle = 0x00040000L;
    private const long ToolWindowExtendedStyle = 0x00000080L;
    private const long NoActivateExtendedStyle = 0x08000000L;
    private static readonly nint TopMostWindow = new(-1);

    private readonly nint _windowHandle;
    private readonly AppWindow _appWindow;

    public NoActivateWindow(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        _windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(window);
        _appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(_windowHandle));
    }

    public void Configure()
    {
        // The window keeps a real frame and only hides its title bar. Stripping WS_CAPTION
        // and WS_THICKFRAME by hand does produce a borderless window, but DWM then stops
        // treating it as an ordinary window: no drop shadow, and no open or close
        // transition. Letting the presenter hide the title bar keeps both, for free and in
        // whatever form the user's Windows is currently using.
        if (_appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(hasBorder: true, hasTitleBar: false);
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsAlwaysOnTop = true;
        }

        // Only the extended styles are ours: stay off the taskbar and out of Alt+Tab, and
        // never take activation.
        var extendedStyle = NativeMethods.GetWindowLongPtrW(_windowHandle, ExtendedStyleIndex).ToInt64();
        extendedStyle = (extendedStyle & ~AppWindowExtendedStyle)
            | ToolWindowExtendedStyle
            | NoActivateExtendedStyle;
        NativeMethods.SetWindowLongPtrW(_windowHandle, ExtendedStyleIndex, new nint(extendedStyle));

        var cornerPreference = 2; // DWMWCP_ROUND
        NativeMethods.DwmSetWindowAttribute(_windowHandle, 33, ref cornerPreference, sizeof(int));
    }

    public void ShowWithoutActivating()
    {
        // AppWindow keeps its own visibility state, and a raw ShowWindow leaves that state
        // disagreeing with the window: the window ends up positioned correctly but without
        // WS_VISIBLE. Show(activateWindow: false) is the supported way to do exactly this.
        _appWindow.Show(activateWindow: false);
        NativeMethods.SetWindowPos(
            _windowHandle,
            TopMostWindow,
            0,
            0,
            0,
            0,
            0x0001 | 0x0002 | 0x0010 | 0x0040); // NOSIZE | NOMOVE | NOACTIVATE | SHOWWINDOW
    }

    public void HideWindow() => _appWindow.Hide();

    public void MoveOffScreen() => _appWindow.Move(new PointInt32(-32000, -32000));

    /// <summary>Work area, in physical pixels, of the display holding the given window.</summary>
    public RectInt32 GetWorkAreaFor(nint sourceWindowHandle)
    {
        var monitor = sourceWindowHandle == 0
            ? NativeMethods.MonitorFromWindow(_windowHandle, 2)
            : NativeMethods.MonitorFromWindow(sourceWindowHandle, 2);
        var info = new NativeMethods.MonitorInfo { Size = Marshal.SizeOf<NativeMethods.MonitorInfo>() };
        if (!NativeMethods.GetMonitorInfoW(monitor, ref info))
        {
            var fallback = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary).WorkArea;
            return fallback;
        }

        return new RectInt32(
            info.WorkAreaLeft,
            info.WorkAreaTop,
            info.WorkAreaRight - info.WorkAreaLeft,
            info.WorkAreaBottom - info.WorkAreaTop);
    }

    /// <param name="logicalSize">Size in device-independent pixels; scaled to the target display.</param>
    public void CenterOn(nint sourceWindowHandle, SizeInt32 logicalSize)
    {
        var workArea = GetWorkAreaFor(sourceWindowHandle);

        // Move onto the target display first so the DPI read below is the one that applies.
        _appWindow.Move(new PointInt32(workArea.X, workArea.Y));
        var dpi = NativeMethods.GetDpiForWindow(_windowHandle);
        var scale = dpi == 0 ? 1d : dpi / 96d;
        var width = Math.Min((int)Math.Round(logicalSize.Width * scale), workArea.Width - 32);
        var height = Math.Min((int)Math.Round(logicalSize.Height * scale), workArea.Height - 32);
        _appWindow.MoveAndResize(new RectInt32(
            workArea.X + ((workArea.Width - width) / 2),
            workArea.Y + ((workArea.Height - height) / 2),
            width,
            height));
    }

    private static class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct MonitorInfo
        {
            public int Size;
            public int MonitorLeft;
            public int MonitorTop;
            public int MonitorRight;
            public int MonitorBottom;
            public int WorkAreaLeft;
            public int WorkAreaTop;
            public int WorkAreaRight;
            public int WorkAreaBottom;
            public uint Flags;
        }

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
        internal static extern nint GetWindowLongPtrW(nint windowHandle, int index);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
        internal static extern nint SetWindowLongPtrW(nint windowHandle, int index, nint value);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetWindowPos(
            nint windowHandle, nint insertAfter, int x, int y, int width, int height, uint flags);

        [DllImport("user32.dll")]
        internal static extern nint MonitorFromWindow(nint windowHandle, uint fallback);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetMonitorInfoW(nint monitor, ref MonitorInfo info);

        [DllImport("user32.dll")]
        internal static extern uint GetDpiForWindow(nint windowHandle);

        [DllImport("dwmapi.dll")]
        internal static extern int DwmSetWindowAttribute(
            nint windowHandle, int attribute, ref int value, int size);
    }
}
