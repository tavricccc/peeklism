using System.Runtime.InteropServices;
using Peeklism.Core.Diagnostics;

namespace Peeklism.App.Services;

/// <summary>
/// The tray presence: Peeklism has no window of its own to return to, so this is the only
/// place a user can pause it or quit it.
/// </summary>
/// <remarks>
/// It owns a hidden window rather than borrowing the preview window. The shell dismisses a
/// tray menu only when its owner can be brought to the foreground, and the preview window
/// is deliberately unable to take focus.
/// </remarks>
public sealed class TrayIcon : IDisposable
{
    private const uint CallbackMessage = 0x8000 + 21;
    private const uint IconId = 1;
    private const uint NotifyAdd = 0;
    private const uint NotifyModify = 1;
    private const uint NotifyDelete = 2;
    private const uint NotifySetVersion = 4;
    private const uint FlagMessage = 0x00000001;
    private const uint FlagIcon = 0x00000002;
    private const uint FlagTip = 0x00000004;
    private const uint IconVersion4 = 4;
    private const uint RightButtonUp = 0x0205;
    private const uint ContextMenu = 0x007B;
    private const uint LeftButtonDoubleClick = 0x0203;
    private const uint NonClientSelect = 0x0400;
    private const uint MenuString = 0x0000;
    private const uint MenuSeparator = 0x0800;
    private const uint MenuChecked = 0x0008;
    private const uint TrackRightButton = 0x0002;
    private const uint TrackReturnCommand = 0x0100;
    private const uint CommandPause = 1;
    private const uint CommandAbout = 2;
    private const uint CommandExit = 3;
    private const string HostWindowClassName = "PeeklismTrayHost";

    private readonly NativeMethods.WindowProcedure _windowProcedure;
    private readonly System.Drawing.Icon _icon;
    private readonly uint _taskbarCreatedMessage;
    private readonly nint _windowHandle;
    private NotifyIconData _iconData;
    private bool _added;
    private bool _disposed;

    public TrayIcon()
    {
        _icon = new System.Drawing.Icon(
            Path.Combine(AppContext.BaseDirectory, "Assets", "Peeklism.ico"), 32, 32);
        _windowProcedure = ProcessMessage;
        _taskbarCreatedMessage = NativeMethods.RegisterWindowMessageW("TaskbarCreated");
        _windowHandle = CreateHostWindow();
        AddIcon();
    }

    /// <summary>Raised when the user asks to pause or resume previewing.</summary>
    public event EventHandler? PauseToggled;

    public event EventHandler? AboutRequested;

    public event EventHandler? ExitRequested;

    /// <summary>Whether previewing is paused, shown as a tick in the menu.</summary>
    public bool IsPaused { get; set; }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_added)
        {
            NativeMethods.Shell_NotifyIconW(NotifyDelete, ref _iconData);
            _added = false;
        }

        if (_windowHandle != 0)
        {
            NativeMethods.DestroyWindow(_windowHandle);
        }

        _icon.Dispose();
    }

    public void UpdateTooltip(string text)
    {
        if (!_added)
        {
            return;
        }

        _iconData.Tip = text;
        NativeMethods.Shell_NotifyIconW(NotifyModify, ref _iconData);
    }

    private nint CreateHostWindow()
    {
        var windowClass = new WindowClass
        {
            Size = (uint)Marshal.SizeOf<WindowClass>(),
            WindowProcedure = Marshal.GetFunctionPointerForDelegate(_windowProcedure),
            InstanceHandle = NativeMethods.GetModuleHandleW(null),
            ClassName = HostWindowClassName,
        };
        NativeMethods.RegisterClassExW(ref windowClass);
        return NativeMethods.CreateWindowExW(
            0x00000080, // WS_EX_TOOLWINDOW: never in the taskbar or Alt+Tab.
            HostWindowClassName,
            "Peeklism",
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            windowClass.InstanceHandle,
            0);
    }

    private void AddIcon()
    {
        _iconData = new NotifyIconData
        {
            Size = (uint)Marshal.SizeOf<NotifyIconData>(),
            WindowHandle = _windowHandle,
            Id = IconId,
            Flags = FlagMessage | FlagIcon | FlagTip,
            CallbackMessage = CallbackMessage,
            IconHandle = _icon.Handle,
            Tip = "Peeklism",
            VersionOrTimeout = IconVersion4,
        };
        _added = NativeMethods.Shell_NotifyIconW(NotifyAdd, ref _iconData);
        if (_added)
        {
            NativeMethods.Shell_NotifyIconW(NotifySetVersion, ref _iconData);
        }
        else
        {
            PeekLog.Write("tray icon could not be added");
        }
    }

    private nint ProcessMessage(nint windowHandle, uint message, nuint wordParameter, nint longParameter)
    {
        // Explorer restarting drops every tray icon, and announces itself so they can be
        // put back.
        if (message == _taskbarCreatedMessage)
        {
            _added = false;
            AddIcon();
            return 0;
        }

        if (message != CallbackMessage)
        {
            return NativeMethods.DefWindowProcW(windowHandle, message, wordParameter, longParameter);
        }

        var notification = unchecked((uint)(longParameter.ToInt64() & 0xFFFF));
        if (notification is RightButtonUp or ContextMenu or LeftButtonDoubleClick or NonClientSelect)
        {
            ShowMenu();
        }

        return 0;
    }

    private void ShowMenu()
    {
        var menu = NativeMethods.CreatePopupMenu();
        if (menu == 0)
        {
            return;
        }

        try
        {
            NativeMethods.AppendMenuW(
                menu,
                MenuString | (IsPaused ? MenuChecked : 0),
                CommandPause,
                "暫停預覽");
            NativeMethods.AppendMenuW(menu, MenuSeparator, 0, null);
            NativeMethods.AppendMenuW(menu, MenuString, CommandAbout, "關於 Peeklism");
            NativeMethods.AppendMenuW(menu, MenuSeparator, 0, null);
            NativeMethods.AppendMenuW(menu, MenuString, CommandExit, "結束 Peeklism");
            NativeMethods.GetCursorPos(out var point);

            // Without this the menu stays on screen after the user clicks elsewhere.
            NativeMethods.SetForegroundWindow(_windowHandle);
            var command = NativeMethods.TrackPopupMenuEx(
                menu,
                TrackRightButton | TrackReturnCommand,
                point.X,
                point.Y,
                _windowHandle,
                0);
            switch (command)
            {
                case CommandPause:
                    PauseToggled?.Invoke(this, EventArgs.Empty);
                    break;
                case CommandAbout:
                    AboutRequested?.Invoke(this, EventArgs.Empty);
                    break;
                case CommandExit:
                    ExitRequested?.Invoke(this, EventArgs.Empty);
                    break;
            }
        }
        finally
        {
            NativeMethods.DestroyMenu(menu);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WindowClass
    {
        public uint Size;
        public uint Style;
        public nint WindowProcedure;
        public int ExtraClassBytes;
        public int ExtraWindowBytes;
        public nint InstanceHandle;
        public nint IconHandle;
        public nint CursorHandle;
        public nint BackgroundBrush;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string? MenuName;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string ClassName;

        public nint SmallIconHandle;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public uint Size;
        public nint WindowHandle;
        public uint Id;
        public uint Flags;
        public uint CallbackMessage;
        public nint IconHandle;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Tip;

        public uint State;
        public uint StateMask;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string Info;

        public uint VersionOrTimeout;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string InfoTitle;

        public uint InfoFlags;
        public Guid ItemGuid;
        public nint BalloonIconHandle;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    private static class NativeMethods
    {
        internal delegate nint WindowProcedure(
            nint windowHandle, uint message, nuint wordParameter, nint longParameter);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool Shell_NotifyIconW(uint message, ref NotifyIconData data);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern uint RegisterWindowMessageW(string message);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern ushort RegisterClassExW(ref WindowClass windowClass);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern nint CreateWindowExW(
            uint extendedStyle,
            string className,
            string windowName,
            uint style,
            int x,
            int y,
            int width,
            int height,
            nint parent,
            nint menu,
            nint instance,
            nint parameter);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern nint DefWindowProcW(
            nint windowHandle, uint message, nuint wordParameter, nint longParameter);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DestroyWindow(nint windowHandle);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        internal static extern nint GetModuleHandleW(string? moduleName);

        [DllImport("user32.dll")]
        internal static extern nint CreatePopupMenu();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool AppendMenuW(nint menu, uint flags, nuint identifier, string? text);

        [DllImport("user32.dll")]
        internal static extern uint TrackPopupMenuEx(
            nint menu, uint flags, int x, int y, nint windowHandle, nint parameters);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DestroyMenu(nint menu);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetCursorPos(out Point point);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetForegroundWindow(nint windowHandle);
    }
}
