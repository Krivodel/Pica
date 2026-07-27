using System.Runtime.InteropServices;

namespace Pica.Viewer.Services;

[ComImport]
[Guid("973810AE-9599-4B88-9E4D-6EE98C9552DA")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IWindowsEnumAssocHandlers
{
    [PreserveSig]
    int Next(
        int count,
        [MarshalAs(UnmanagedType.Interface)] out IWindowsAssocHandler? handler,
        out int fetched);
}
