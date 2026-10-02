using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Pica.Desktop.Services;

internal sealed class WindowsClipboardHotKey : IAsyncDisposable
{
    private const int FirstRegistration = 1;
    private const int SecondRegistration = 2;
    private const int ProbeRegistration = 3;
    private readonly ConcurrentQueue<Action> _changes = new();
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Action _activated;
    private PicaClipboardShortcutGesture? _gesture;
    private uint _threadId;
    private int _registration;
    private int _disposed;

    internal WindowsClipboardHotKey(Action activated)
    {
        _activated = activated ?? throw new ArgumentNullException(nameof(activated));
        Thread thread = new(Run) { IsBackground = true, Name = "Pica global clipboard hotkey" };
        thread.Start();
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            await _ready.Task.ConfigureAwait(false);
            WindowsShortcutNative.PostThreadMessageW(_threadId, WindowsShortcutNative.Quit, nint.Zero, nint.Zero);
        }

        await _completion.Task.ConfigureAwait(false);
    }

    internal async Task ValidateAsync(PicaClipboardShortcutGesture gesture, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(gesture);
        await InvokeAsync(() =>
        {
            if (gesture.HasSameRegistration(_gesture))
            {
                return;
            }

            Register(ProbeRegistration, gesture);
            Unregister(ProbeRegistration);
        }, ct).ConfigureAwait(false);
    }

    internal async Task SetAsync(PicaClipboardShortcutGesture? gesture, CancellationToken ct)
    {
        await InvokeAsync(() =>
        {
            if ((gesture is null) && (_gesture is null))
            {
                return;
            }

            if (gesture?.HasSameRegistration(_gesture) == true)
            {
                _gesture = gesture;
                return;
            }

            int nextRegistration = _registration == FirstRegistration ? SecondRegistration : FirstRegistration;

            if (gesture is not null)
            {
                Register(nextRegistration, gesture);
            }

            try
            {
                if (_gesture is not null)
                {
                    Unregister(_registration);
                }
            }
            catch (Exception)
            {
                if (gesture is not null)
                {
                    Unregister(nextRegistration);
                }

                throw;
            }

            _registration = nextRegistration;
            _gesture = gesture;
        }, ct).ConfigureAwait(false);
    }

    private static void Register(int id, PicaClipboardShortcutGesture gesture)
    {
        if (!WindowsShortcutNative.RegisterHotKey(nint.Zero, id,
                gesture.RegistrationModifiers | WindowsShortcutNative.NoRepeat, (uint)gesture.VirtualKey))
        {
            int error = Marshal.GetLastWin32Error();

            if (error == WindowsShortcutNative.HotKeyAlreadyRegistered)
            {
                throw new PicaShortcutException(PicaShortcutFailure.Occupied);
            }

            throw new Win32Exception(error, "Pica could not register the clipboard hotkey.");
        }
    }

    private static void Unregister(int id)
    {
        if (!WindowsShortcutNative.UnregisterHotKey(nint.Zero, id))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Pica could not release the clipboard hotkey.");
        }
    }

    private async Task InvokeAsync(Action change, CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        await _ready.Task.WaitAsync(ct).ConfigureAwait(false);
        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _changes.Enqueue(() =>
        {
            try
            {
                ct.ThrowIfCancellationRequested();
                change();
                completion.SetResult();
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
        });

        if (!WindowsShortcutNative.PostThreadMessageW(_threadId, WindowsShortcutNative.Apply, nint.Zero, nint.Zero))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Pica hotkey thread is unavailable.");
        }

        Task finished = await Task.WhenAny(completion.Task, _completion.Task).ConfigureAwait(false);

        if ((finished == _completion.Task) && !completion.Task.IsCompleted)
        {
            await _completion.Task.ConfigureAwait(false);
            throw new ObjectDisposedException(nameof(WindowsClipboardHotKey));
        }

        await completion.Task.ConfigureAwait(false);
    }

    private void Run()
    {
        Exception? failure = null;

        try
        {
            _threadId = WindowsShortcutNative.GetCurrentThreadId();
            WindowsShortcutNative.PeekMessageW(out _, nint.Zero, 0, 0, 0);
            _ready.SetResult();

            while (true)
            {
                int result = WindowsShortcutNative.GetMessageW(out WindowsShortcutMessage message, nint.Zero, 0, 0);

                if (result < 0)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Pica could not read hotkey messages.");
                }

                if (result == 0)
                {
                    break;
                }

                if (message.Message == WindowsShortcutNative.Apply)
                {
                    while (_changes.TryDequeue(out Action? change))
                    {
                        change();
                    }
                }
                else if ((message.Message == WindowsShortcutNative.HotKey)
                    && (message.Parameter == (nuint)_registration) && (_gesture is not null))
                {
                    _activated();
                }
            }
        }
        catch (Exception ex)
        {
            _ready.TrySetException(ex);
            failure = ex;
        }
        finally
        {
            if (_gesture is not null)
            {
                try
                {
                    Unregister(_registration);
                }
                catch (Exception ex)
                {
                    failure ??= ex;
                }
            }

            if (failure is null)
            {
                _completion.TrySetResult();
            }
            else
            {
                _completion.TrySetException(failure);
            }
        }
    }
}
