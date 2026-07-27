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
}
