using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Pica.Desktop.Services;

internal static class WindowsShortcutNative
{
    internal const int KeyboardHook = 13;
    internal const uint KeyDown = 0x0100;
    internal const uint KeyUp = 0x0101;
    internal const uint SystemKeyDown = 0x0104;
    internal const uint SystemKeyUp = 0x0105;
    internal const uint Quit = 0x0012;
    internal const uint HotKey = 0x0312;
    internal const uint Apply = 0x8001;
    internal const uint NoRepeat = 0x4000;
    internal const int ExtendedKeyFlag = 1;
    internal const int HotKeyAlreadyRegistered = 1409;
    internal const uint SystemCommand = 0x0112;
    internal const uint SetWindowHotKeyMessage = 0x0032;
    internal const uint GetWindowHotKeyMessage = 0x0033;
    internal const int ShortcutSystemCommand = 0xF150;
    internal const int SystemCommandMask = 0xFFF0;

    private const int ShellItemCreated = 0x00000002;
    private const int ShellItemDeleted = 0x00000004;
    private const int ShellItemUpdated = 0x00002000;
    private const uint ShellUnicodePath = 0x0005;
    private const uint ShellFlush = 0x1000;
    private const uint AbortIfHung = 0x0002;
    private const uint QueryTimeoutMilliseconds = 50;

    internal delegate nint KeyboardHookCallback(int code, nint parameter, nint data);

    internal static void NotifyShortcutChanged(string path, bool existed, bool exists)
    {
        int change = exists ? (existed ? ShellItemUpdated : ShellItemCreated) : ShellItemDeleted;
        SHChangeNotify(change, ShellUnicodePath | ShellFlush, path, nint.Zero);
    }

    internal static void ClearWindowHotKey(nint window)
    {
        SendMessageTimeoutW(window, SetWindowHotKeyMessage, nint.Zero, nint.Zero,
            AbortIfHung, QueryTimeoutMilliseconds, out _);
    }

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern nint SetWindowsHookExW(int hook, KeyboardHookCallback callback, nint module, uint thread);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnhookWindowsHookEx(nint hook);

    [DllImport("user32.dll")]
    internal static extern nint CallNextHookEx(nint hook, int code, nint parameter, nint data);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern int GetMessageW(out WindowsShortcutMessage message, nint window, uint minimum, uint maximum);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool PeekMessageW(out WindowsShortcutMessage message, nint window, uint minimum, uint maximum, uint remove);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool PostThreadMessageW(uint thread, uint message, nint parameter, nint data);

    [DllImport("user32.dll")]
    internal static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    internal static extern short GetAsyncKeyState(int key);

    [DllImport("kernel32.dll")]
    internal static extern uint GetCurrentThreadId();

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    internal static extern nint GetModuleHandleW(string? name);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint key);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnregisterHotKey(nint window, int id);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool AllowSetForegroundWindow(int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out int processId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SendMessageTimeoutW(nint window, uint message, nint parameter, nint data,
        uint flags, uint timeout, out nuint result);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern void SHChangeNotify(int eventId, uint flags, string item, nint otherItem);
}
