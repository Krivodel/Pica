using System.Runtime.InteropServices;

namespace Pica.Desktop.Services;

[StructLayout(LayoutKind.Sequential)]
internal struct WindowsShortcutKeyEvent
{
    internal int VirtualKey;
    internal int ScanCode;
    internal int Flags;
    internal int Time;
    internal nuint ExtraInformation;
}
