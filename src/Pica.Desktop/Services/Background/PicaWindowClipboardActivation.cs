using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;

using Microsoft.Extensions.Logging;

using Pica.Viewer.Views;

namespace Pica.Desktop.Services.Background;

internal sealed class PicaWindowClipboardActivation : IDisposable
{
    internal Task Completion => _completion;

    private readonly CancellationTokenSource _closing = new();
    private readonly ImageViewerWindow _window;
    private readonly IPicaDesktopStateService _stateService;
    private readonly PicaClipboardShortcutService _shortcutService;
    private readonly ILogger<PicaWindowClipboardActivation> _logger;
    private readonly Task _completion;
    private nint _handle;
    private bool _disposed;

    internal PicaWindowClipboardActivation(ImageViewerWindow window, IPicaDesktopStateService stateService,
        PicaClipboardShortcutService shortcutService, ILogger<PicaWindowClipboardActivation> logger)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _stateService = stateService ?? throw new ArgumentNullException(nameof(stateService));
        _shortcutService = shortcutService ?? throw new ArgumentNullException(nameof(shortcutService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _window.Opened += OnOpened;
        _window.Activated += OnActivated;
        Win32Properties.AddWndProcHookCallback(_window, OnWindowMessage);
        _completion = ListenAsync();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _window.Opened -= OnOpened;
        _window.Activated -= OnActivated;
        Win32Properties.RemoveWndProcHookCallback(_window, OnWindowMessage);
        _closing.Cancel();

        if (_handle != nint.Zero)
        {
            WindowsPicaActivationLocator.Remove(_handle);
        }
    }

    internal static bool IsWindowHotKeyAssignment(uint message, nint hotKey)
    {
        return (message == WindowsShortcutNative.SetWindowHotKeyMessage) && (hotKey != nint.Zero);
    }

    internal static void ReleaseWindowHotKey(nint handle)
    {
        if (handle != nint.Zero)
        {
            WindowsShortcutNative.ClearWindowHotKey(handle);
        }
    }

    private async Task ListenAsync()
    {
        PicaActivationServer server = new(PicaBackgroundActivationEndpoint.ForProcess(Environment.ProcessId));

        try
        {
            while (!_closing.IsCancellationRequested)
            {
                try
                {
                    await using IPicaBackgroundActivation activation = await server.ReceiveAsync(_closing.Token)
                        .ConfigureAwait(false);
                    PicaDesktopState state = await _stateService.LoadAsync(_closing.Token).ConfigureAwait(false);
                    await _window.Dispatcher.InvokeAsync(() =>
                    {
                        _shortcutService.Refresh(state);
                        _window.SetClipboardShortcut(state.IsClipboardShortcutEnabled ? state.ClipboardShortcut : null);
                        RefreshWindowHotKey();
                    }, DispatcherPriority.Normal, _closing.Token);

                    if (PicaLaunchArguments.IsClipboard(activation.Arguments))
                    {
                        Task paste = await _window.Dispatcher.InvokeAsync(ActivateAndPasteAsync,
                            DispatcherPriority.Normal, _closing.Token);
                        await activation.AcknowledgeAsync(_closing.Token).ConfigureAwait(false);
                        _ = ObservePasteAsync(paste);
                    }
                    else
                    {
                        await activation.AcknowledgeAsync(_closing.Token).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) when (_closing.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Pica viewer clipboard activation failed");
                }
            }
        }
        finally
        {
            _closing.Dispose();
        }
    }

    private async Task ObservePasteAsync(Task paste)
    {
        try
        {
            await paste.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_disposed)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pica clipboard insertion failed");
        }
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        _handle = _window.TryGetPlatformHandle()?.Handle ?? nint.Zero;
        WindowsPicaActivationLocator.MarkAvailable(_handle);
        WindowsPicaActivationLocator.MarkActive(_handle);
        RefreshWindowHotKey();
    }

    private void OnActivated(object? sender, EventArgs e)
    {
        if (_handle != nint.Zero)
        {
            WindowsPicaActivationLocator.MarkActive(_handle);
            RefreshWindowHotKey();
        }
    }

    private async Task ActivateAndPasteAsync()
    {
        if (_disposed)
        {
            return;
        }

        if (_window.WindowState == WindowState.Minimized)
        {
            _window.WindowState = WindowState.Normal;
        }

        _window.Activate();
        await _window.PasteFromClipboardAsync(_closing.Token);
    }

    private nint OnWindowMessage(nint window, uint message, nint parameter, nint data, ref bool handled)
    {
        PicaDesktopState state = _shortcutService.CurrentState;

        if (IsWindowHotKeyAssignment(message, parameter))
        {
            // Window hotkeys focus Pica without delivering --clipboard; Explorer must keep the shortcut.
            handled = true;
        }
        else if ((message == WindowsShortcutNative.SystemCommand)
            && ((parameter.ToInt64() & WindowsShortcutNative.SystemCommandMask) == WindowsShortcutNative.ShortcutSystemCommand)
            && (data == window))
        {
            handled = true;

            if (state.IsClipboardShortcutEnabled && !state.RequiresClipboardAgent && !_disposed)
            {
                _window.Dispatcher.Post(() => _ = ObservePasteAsync(ActivateAndPasteAsync()));
            }
        }

        return nint.Zero;
    }

    private void RefreshWindowHotKey()
    {
        if ((_handle == nint.Zero) || _disposed)
        {
            return;
        }

        ReleaseWindowHotKey(_handle);
    }
}
