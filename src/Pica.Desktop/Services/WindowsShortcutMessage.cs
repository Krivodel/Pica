using System.Runtime.InteropServices;

namespace Pica.Desktop.Services;

[StructLayout(LayoutKind.Sequential)]
internal struct WindowsShortcutMessage
{
    internal nint Window;
    internal uint Message;
    internal nuint Parameter;
    internal nint Data;
    internal uint Time;
    internal int PointX;
    internal int PointY;
    internal uint Private;
}
