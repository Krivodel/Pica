using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Pica.Viewer.Services;

[SupportedOSPlatform("windows")]
internal static class WindowsComObject
{
    internal static void Release(object value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (Marshal.IsComObject(value))
        {
            Marshal.FinalReleaseComObject(value);
        }
    }
}
