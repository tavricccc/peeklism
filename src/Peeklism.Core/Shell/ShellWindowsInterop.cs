using System.Runtime.InteropServices;

namespace Peeklism.Core.Shell;

/// <summary>
/// Minimal projection of the shell automation objects Peeklism needs.
/// </summary>
/// <remarks>
/// Explorer registers every one of its windows — the desktop included — in a process-wide
/// collection, which is why reading their selection needs no code injection. Common file
/// dialogs are not registered, and are handled by the native helper instead.
/// </remarks>
internal static class ShellWindowsInterop
{
    /// <summary>CLSID_ShellWindows.</summary>
    private static readonly Guid ShellWindowsClassId = new("9BA05972-F6A8-11CF-A442-00A0C90A8F39");

    /// <summary>SWC_DESKTOP: ask for the desktop's own shell view.</summary>
    internal const int ShellWindowClassDesktop = 8;

    /// <summary>SWFO_NEEDDISPATCH: return the automation object, not only the window handle.</summary>
    internal const int FindWindowNeedDispatch = 1;

    internal static IShellWindows? Create()
    {
        var type = Type.GetTypeFromCLSID(ShellWindowsClassId);
        return type is null ? null : Activator.CreateInstance(type) as IShellWindows;
    }
}

/// <summary>
/// IShellWindows. Every member is declared, in declaration order, because the CLR calls
/// through the v-table: omitting an entry would silently invoke the wrong slot.
/// </summary>
[ComImport]
[Guid("85CB6900-4D95-11CF-960C-0080C7F4EE85")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
internal interface IShellWindows
{
    int Count { get; }

    [return: MarshalAs(UnmanagedType.IDispatch)]
    object? Item([MarshalAs(UnmanagedType.Struct)] object index);

    [return: MarshalAs(UnmanagedType.IUnknown)]
    object? _NewEnum();

    void Register(
        [MarshalAs(UnmanagedType.IDispatch)] object shellWindow,
        int windowHandle,
        int shellWindowClass,
        out int cookie);

    void RegisterPending(
        int threadId,
        [MarshalAs(UnmanagedType.Struct)] ref object location,
        [MarshalAs(UnmanagedType.Struct)] ref object locationRoot,
        int shellWindowClass,
        out int cookie);

    void Revoke(int cookie);

    void OnNavigate(int cookie, [MarshalAs(UnmanagedType.Struct)] ref object location);

    void OnActivated(int cookie, [MarshalAs(UnmanagedType.VariantBool)] bool active);

    [return: MarshalAs(UnmanagedType.IDispatch)]
    object? FindWindowSW(
        [MarshalAs(UnmanagedType.Struct)] ref object location,
        [MarshalAs(UnmanagedType.Struct)] ref object locationRoot,
        int shellWindowClass,
        out int windowHandle,
        int options);

    void OnCreated(int cookie, [MarshalAs(UnmanagedType.IUnknown)] object shellWindow);

    void ProcessAttachDetach([MarshalAs(UnmanagedType.VariantBool)] bool attach);
}
