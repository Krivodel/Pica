using System.Runtime.InteropServices;

namespace Pica.Desktop.Services;

internal static class WindowsForegroundWindowCapture
{
    internal static long? Capture()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        nint windowHandle = GetForegroundWindow();

        return windowHandle == nint.Zero
            ? null
            : windowHandle.ToInt64();
    }

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();
}
