using Microsoft.Extensions.Logging;

namespace Pica.Desktop.Services;

internal sealed class WindowsShortcutRecorder : IPicaShortcutRecorder
{
    private readonly ILogger<WindowsShortcutRecorder> _logger;
    private readonly SemaphoreSlim _recordingLock = new(1, 1);

    public WindowsShortcutRecorder(ILogger<WindowsShortcutRecorder> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PicaClipboardShortcutGesture> RecordAsync(nint owner, CancellationToken ct)
    {
        if (!OperatingSystem.IsWindows() || (owner == nint.Zero))
        {
            throw new PlatformNotSupportedException("Shortcut recording requires a Windows owner window.");
        }

        await _recordingLock.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            ct.ThrowIfCancellationRequested();

            if (WindowsShortcutNative.GetForegroundWindow() != owner)
            {
                throw new OperationCanceledException("Pica shortcut recording window is no longer active.");
            }

            WindowsShortcutRecordingSession session = new(WindowsShortcutKeyCatalog.ReadPressedModifiers());
            WindowsShortcutHook? recordingHook = null;
            await using WindowsShortcutHook hook = new((input, message) =>
            {
                if (WindowsShortcutNative.GetForegroundWindow() != owner)
                {
                    recordingHook?.Dispose();
                    return false;
                }

                session.ProcessKey(input.VirtualKey,
                    message is WindowsShortcutNative.KeyDown or WindowsShortcutNative.SystemKeyDown,
                    (input.Flags & WindowsShortcutNative.ExtendedKeyFlag) != 0);

                if (session.IsComplete || session.IsCanceled)
                {
                    recordingHook?.Dispose();
                }

                return true;
            });
            recordingHook = hook;
            await hook.StartAsync(ct).ConfigureAwait(false);
            await hook.Completion.WaitAsync(ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();

            if (session.IsCanceled || !session.IsComplete || (session.Gesture is null))
            {
                throw new OperationCanceledException("Pica shortcut recording was canceled.");
            }

            return session.Gesture;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pica shortcut recording failed");
            throw;
        }
        finally
        {
            _recordingLock.Release();
        }
    }
}
