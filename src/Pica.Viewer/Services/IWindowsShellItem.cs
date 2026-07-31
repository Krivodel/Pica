using System.Runtime.InteropServices;

namespace Pica.Viewer.Services;

[ComImport]
[Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IWindowsShellItem
{
    [PreserveSig]
    int BindToHandler(
        nint bindContext,
        ref Guid handlerId,
        ref Guid interfaceId,
        out nint result);

    [PreserveSig]
    int GetParent(
        [MarshalAs(UnmanagedType.Interface)] out IWindowsShellItem? parent);

    [PreserveSig]
    int GetDisplayName(
        WindowsShellItemDisplayName displayName,
        out nint name);

    [PreserveSig]
    int GetAttributes(uint attributeMask, out uint attributes);

    [PreserveSig]
    int Compare(
        IWindowsShellItem shellItem,
        uint hint,
        out int order);
}
