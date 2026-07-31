using System.Runtime.InteropServices;

namespace Pica.Viewer.Services.FileReveal;

[ComImport]
[Guid("6D5140C1-7436-11CE-8034-00AA006009FA")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IWindowsOleServiceProvider
{
    [PreserveSig]
    int QueryService(
        ref Guid serviceId,
        ref Guid interfaceId,
        [MarshalAs(UnmanagedType.Interface)] out object? service);
}
