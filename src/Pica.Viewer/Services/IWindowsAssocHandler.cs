using System.Runtime.InteropServices;

namespace Pica.Viewer.Services;

[ComImport]
[Guid("F04061AC-1659-4A3F-A954-775AA57FC083")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IWindowsAssocHandler
{
    [PreserveSig]
    int GetName(out nint name);

    [PreserveSig]
    int GetUIName(out nint name);

    [PreserveSig]
    int GetIconLocation(out nint path, out int index);

    [PreserveSig]
    int IsRecommended();

    [PreserveSig]
    int MakeDefault([MarshalAs(UnmanagedType.LPWStr)] string description);

    [PreserveSig]
    int Invoke(nint dataObject);

    [PreserveSig]
    int CreateInvoker(
        nint dataObject,
        out nint invoker);
}
