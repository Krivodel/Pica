using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Pica.Desktop.Services;

internal sealed class WindowsShortcutHook : IDisposable, IAsyncDisposable
{
    internal bool IsRunning => _isRunning;
    internal Task Completion => _completion.Task;

    private readonly CancellationTokenSource _closing = new();
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Func<WindowsShortcutKeyEvent, uint, bool> _process;
    private volatile bool _isRunning;
    private int _started;
    private int _disposed;

    internal WindowsShortcutHook(Func<WindowsShortcutKeyEvent, uint, bool> process)
    {
        _process = process ?? throw new ArgumentNullException(nameof(process));
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _closing.Cancel();

            if (Volatile.Read(ref _started) == 0)
            {
                _completion.TrySetResult();
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        Dispose();

        try
        {
            await Completion.ConfigureAwait(false);
        }
        finally
        {
            _closing.Dispose();
        }
    }

    internal async Task StartAsync(CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

        if (Interlocked.Exchange(ref _started, 1) != 0)
        {
            throw new InvalidOperationException("The Windows shortcut hook has already started.");
        }

        Thread thread = new(Run) { IsBackground = true, Name = "Pica shortcut input" };
        thread.Start();

        try
        {
            await _ready.Task.WaitAsync(ct).ConfigureAwait(false);
        }
        catch (Exception)
        {
            Dispose();
            throw;
        }
    }

    private void Run()
    {
        try
        {
            ReadMessages();
            _completion.TrySetResult();
        }
        catch (Exception ex)
        {
            _ready.TrySetException(ex);
            _completion.TrySetException(ex);
        }
    }

    private void ReadMessages()
    {
        uint threadId = WindowsShortcutNative.GetCurrentThreadId();
        WindowsShortcutNative.PeekMessageW(out _, nint.Zero, 0, 0, 0);
        Exception? callbackFailure = null;
        WindowsShortcutNative.KeyboardHookCallback callback = (code, parameter, data) =>
        {
            uint message = unchecked((uint)parameter.ToInt64());

            if ((code >= 0) && (message is WindowsShortcutNative.KeyDown or WindowsShortcutNative.SystemKeyDown
                or WindowsShortcutNative.KeyUp or WindowsShortcutNative.SystemKeyUp))
            {
                try
                {
                    if (_process(Marshal.PtrToStructure<WindowsShortcutKeyEvent>(data), message))
                    {
                        return (nint)1;
                    }
                }
                catch (Exception ex)
                {
                    callbackFailure = ex;
                    WindowsShortcutNative.PostThreadMessageW(threadId, WindowsShortcutNative.Quit, nint.Zero, nint.Zero);
                }
            }

            return WindowsShortcutNative.CallNextHookEx(nint.Zero, code, parameter, data);
        };
        nint hook = WindowsShortcutNative.SetWindowsHookExW(WindowsShortcutNative.KeyboardHook, callback,
            WindowsShortcutNative.GetModuleHandleW(null), 0);

        if (hook == nint.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Pica could not start Windows shortcut input.");
        }

        try
        {
            using CancellationTokenRegistration closing = _closing.Token.Register(() =>
                WindowsShortcutNative.PostThreadMessageW(threadId, WindowsShortcutNative.Quit, nint.Zero, nint.Zero));
            _isRunning = true;
            _ready.TrySetResult();

            while (!_closing.IsCancellationRequested)
            {
                int result = WindowsShortcutNative.GetMessageW(out _, nint.Zero, 0, 0);

                if (result < 0)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Pica could not read Windows shortcut messages.");
                }

                if (result == 0)
                {
                    break;
                }
            }

            if (callbackFailure is not null)
            {
                throw new InvalidOperationException("Pica could not process Windows shortcut input.", callbackFailure);
            }
        }
        finally
        {
            _isRunning = false;

            bool unhooked = WindowsShortcutNative.UnhookWindowsHookEx(hook);
            int error = Marshal.GetLastWin32Error();
            GC.KeepAlive(callback);

            if (!unhooked)
            {
                throw new Win32Exception(error, "Pica could not release Windows shortcut input.");
            }
        }
    }
}
