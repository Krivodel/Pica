using System.ComponentModel;
using System.Runtime.InteropServices;

using Pica.Desktop.Services;
using Pica.Desktop.Services.Background;

namespace Pica.Desktop.Tests.Services;

internal sealed class WindowsShortcutTestWindow : IAsyncDisposable
{
    internal Task<nint> Handle => _ready.Task;

    private const int VisiblePopupStyle = unchecked((int)0x90000000);
    private const int WindowProcedureIndex = -4;
    private const uint KeyReleased = 2;
    private readonly bool _rejectWindowHotKeys;
    private readonly WindowProcedure _windowProcedure;
    private readonly TaskCompletionSource<nint> _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private uint _threadId;
    private nint _originalProcedure;

    internal WindowsShortcutTestWindow(bool rejectWindowHotKeys = false)
    {
        _rejectWindowHotKeys = rejectWindowHotKeys;
        _windowProcedure = OnWindowMessage;
        Thread thread = new(Run) { IsBackground = true, Name = "Pica shortcut test" };
        thread.Start();
    }

    internal static void PressKey(int virtualKey)
    {
        keybd_event((byte)virtualKey, 0, 0, 0);
        keybd_event((byte)virtualKey, 0, KeyReleased, 0);
    }

    internal static void Activate(nint handle)
    {
        SetForegroundWindow(handle);
    }

    internal static short ReadHotKey(nint handle)
    {
        return unchecked((short)SendMessageW(handle, WindowsShortcutNative.GetWindowHotKeyMessage, nint.Zero, nint.Zero));
    }

    internal static nint SetHotKey(nint handle, short hotKey)
    {
        return SendMessageW(handle, WindowsShortcutNative.SetWindowHotKeyMessage, hotKey, nint.Zero);
    }

    public async ValueTask DisposeAsync()
    {
        await _ready.Task.ConfigureAwait(false);
        WindowsShortcutNative.PostThreadMessageW(_threadId, WindowsShortcutNative.Quit, nint.Zero, nint.Zero);
        await _closed.Task.ConfigureAwait(false);
    }

    private void Run()
    {
        nint handle = nint.Zero;

        try
        {
            _threadId = WindowsShortcutNative.GetCurrentThreadId();
            handle = CreateWindowExW(0, "STATIC", "Pica shortcut test", VisiblePopupStyle,
                0, 0, GetSystemMetrics(0), GetSystemMetrics(1), nint.Zero, nint.Zero, nint.Zero, nint.Zero);

            if (handle == nint.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "The shortcut test window could not be created.");
            }

            _originalProcedure = SetWindowLongPtrW(handle, WindowProcedureIndex,
                Marshal.GetFunctionPointerForDelegate(_windowProcedure));

            if (_originalProcedure == nint.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "The test window procedure could not be attached.");
            }

            _ready.SetResult(handle);

            while (WindowsShortcutNative.GetMessageW(out WindowsShortcutMessage message, nint.Zero, 0, 0) > 0)
            {
                TranslateMessage(ref message);
                DispatchMessageW(ref message);
            }
        }
        catch (Exception ex)
        {
            _ready.TrySetException(ex);
            _closed.TrySetException(ex);
        }
        finally
        {
            if (handle != nint.Zero)
            {
                DestroyWindow(handle);
            }

            _closed.TrySetResult();
        }
    }

    private nint OnWindowMessage(nint window, uint message, nint parameter, nint data)
    {
        if (_rejectWindowHotKeys && PicaWindowClipboardActivation.IsWindowHotKeyAssignment(message, parameter))
        {
            return nint.Zero;
        }

        return CallWindowProcW(_originalProcedure, window, message, parameter, data);
    }

    private delegate nint WindowProcedure(nint window, uint message, nint parameter, nint data);

    [DllImport("user32.dll")]
    private static extern nint SendMessageW(nint window, uint message, nint parameter, nint data);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowLongPtrW(nint window, int index, nint value);

    [DllImport("user32.dll")]
    private static extern nint CallWindowProcW(nint procedure, nint window, uint message, nint parameter, nint data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateWindowExW(int extendedStyle, string className, string title, int style,
        int left, int top, int width, int height, nint parent, nint menu, nint instance, nint parameter);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint window);

    [DllImport("user32.dll")]
    private static extern nint DispatchMessageW(ref WindowsShortcutMessage message);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TranslateMessage(ref WindowsShortcutMessage message);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(nint window);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, nuint information);
}
