using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Pica.Desktop.Services.Background;

internal static class WindowsPicaActivationLocator
{
    private const string ActivityProperty = "Pica.Clipboard.LastActive.v1";
    private const string AvailabilityProperty = "Pica.Clipboard.Available.v1";

    internal static PicaBackgroundActivationEndpoint? FindLastActive()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        return FindWindows().Where(window => window.Value > 0).OrderByDescending(window => window.Value)
            .Select(window => window.Key).FirstOrDefault();
    }

    internal static IReadOnlyList<PicaBackgroundActivationEndpoint> FindAll()
    {
        return FindWindows().Keys.ToArray();
    }

    internal static void MarkActive(nint window)
    {
        if (!SetProp(window, ActivityProperty, (nint)Stopwatch.GetTimestamp()))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Pica could not publish window activation.");
        }
    }

    internal static void MarkAvailable(nint window)
    {
        if (!SetProp(window, AvailabilityProperty, (nint)1))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Pica could not publish clipboard activation availability.");
        }
    }

    internal static void Remove(nint window)
    {
        RemoveProp(window, ActivityProperty);
        RemoveProp(window, AvailabilityProperty);
    }

    internal static void ClearShortcutHotKeys()
    {
        foreach (nint window in EnumerateWindows())
        {
            if (GetProp(window, AvailabilityProperty) != nint.Zero)
            {
                WindowsShortcutNative.ClearWindowHotKey(window);
            }
        }
    }

    private static IReadOnlyDictionary<PicaBackgroundActivationEndpoint, long> FindWindows()
    {
        Dictionary<PicaBackgroundActivationEndpoint, long> windows = [];

        if (!OperatingSystem.IsWindows())
        {
            return windows;
        }

        foreach (nint window in EnumerateWindows())
        {
            PicaBackgroundActivationEndpoint? candidate = GetEndpoint(window, out long activity);

            if (candidate is not null)
            {
                windows[candidate] = Math.Max(windows.GetValueOrDefault(candidate), activity);
            }
        }

        return windows;
    }

    private static IReadOnlyList<nint> EnumerateWindows()
    {
        List<nint> windows = [];
        bool completed = EnumWindows((window, _) =>
        {
            windows.Add(window);
            return true;
        }, nint.Zero);

        if (!completed)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Pica could not enumerate windows for shortcut activation.");
        }

        return windows;
    }

    private static PicaBackgroundActivationEndpoint? GetEndpoint(nint window, out long activity)
    {
        activity = GetProp(window, ActivityProperty).ToInt64();

        if ((activity <= 0) && (GetProp(window, AvailabilityProperty) == nint.Zero))
        {
            return null;
        }

        int processId = GetProcessId(window);
        PicaBackgroundActivationEndpoint candidate = PicaBackgroundActivationEndpoint.ForProcess(processId);

        if (!Mutex.TryOpenExisting(candidate.AvailabilityMutexName, out Mutex? availability))
        {
            return null;
        }

        availability.Dispose();
        return candidate;
    }

    private static int GetProcessId(nint window)
    {
        GetWindowThreadProcessId(window, out int processId);

        return processId;
    }

    private delegate bool EnumerateWindow(nint window, nint parameter);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumerateWindow callback, nint parameter);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetProp(nint window, string name);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetProp(nint window, string name, nint value);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint RemoveProp(nint window, string name);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out int processId);
}
