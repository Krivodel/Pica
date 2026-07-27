using System.Runtime.InteropServices;

namespace Pica.Viewer.Services;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal sealed class WindowsOpenAsInfo
{
    [MarshalAs(UnmanagedType.LPWStr)]
    internal required string FilePath;

    [MarshalAs(UnmanagedType.LPWStr)]
    internal string? FileClass;

    internal WindowsOpenAsFlags Flags;
}
