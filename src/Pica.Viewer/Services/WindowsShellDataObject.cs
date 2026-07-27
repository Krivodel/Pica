using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Pica.Viewer.Services;

[SupportedOSPlatform("windows")]
internal sealed class WindowsShellDataObject : IDisposable
{
    internal nint Pointer { get; }

    private static readonly Guid DataObjectHandlerId =
        new("B8C0BD9F-ED24-455C-83E6-D5390C4FE8C4");
    private static readonly Guid DataObjectInterfaceId =
        new("0000010E-0000-0000-C000-000000000046");

    private WindowsShellDataObject(nint pointer)
    {
        Pointer = pointer;
    }

    public void Dispose()
    {
        if (Pointer != nint.Zero)
        {
            Marshal.Release(Pointer);
        }
    }

    internal static WindowsShellDataObject Create(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        Guid shellItemInterfaceId = typeof(IWindowsShellItem).GUID;
        int result = WindowsShellNativeMethods.SHCreateItemFromParsingName(
            filePath,
            nint.Zero,
            ref shellItemInterfaceId,
            out IWindowsShellItem shellItem);
        Marshal.ThrowExceptionForHR(result);

        try
        {
            Guid handlerId = DataObjectHandlerId;
            Guid interfaceId = DataObjectInterfaceId;
            result = shellItem.BindToHandler(
                nint.Zero,
                ref handlerId,
                ref interfaceId,
                out nint dataObject);
            Marshal.ThrowExceptionForHR(result);

            return new WindowsShellDataObject(dataObject);
        }
        finally
        {
            WindowsComObject.Release(shellItem);
        }
    }
}
