using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;

namespace Pica.Viewer.Services;

internal sealed class AvaloniaClipboardDataWriter : IDisposable
{
    private readonly ViewerWindowPlatformContext _platformContext;
    private readonly ILogger<AvaloniaClipboardDataWriter> _logger;
    private IAsyncDataTransfer? _clipboardDataTransfer;
    private Task? _flushTask;
    private CancellationTokenSource? _flushCancellation;
    private bool _hasPendingClipboardData;
    private bool _isFlushStarted;

    public AvaloniaClipboardDataWriter(
        ViewerWindowPlatformContext platformContext,
        ILogger<AvaloniaClipboardDataWriter> logger)
    {
        _platformContext = platformContext
            ?? throw new ArgumentNullException(nameof(platformContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task SetFileAsync(IStorageFile file, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(file);

        DataTransfer dataTransfer = new();
        dataTransfer.Add(DataTransferItem.CreateFile(file));
        await SetDataAsync(dataTransfer, ct);
    }

    public async Task SetBytesAsync(
        DataFormat<byte[]> format,
        byte[] content,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(content);

        DataTransferItem item = new();
        item.Set(format, content);
        DataTransfer dataTransfer = new();
        dataTransfer.Add(item);
        await SetDataAsync(dataTransfer, ct);
    }

    public async Task FlushAsync(CancellationToken ct)
    {
        IClipboard? clipboard = await _platformContext
            .GetClipboardAsync(ct)
            .ConfigureAwait(false);

        if (clipboard is null)
        {
            return;
        }

        Task flushTask = await Dispatcher.UIThread.InvokeAsync(
            () => GetOrStartFlushTask(clipboard, ct),
            DispatcherPriority.Normal,
            ct);
        await flushTask;
    }

    public async Task ReleasePendingDataAsync(CancellationToken ct)
    {
        Task releaseTask = await Dispatcher.UIThread.InvokeAsync(
            () => ReleasePendingDataOnUiThreadAsync(ct),
            DispatcherPriority.Normal,
            ct);
        await releaseTask;
    }

    public void Dispose()
    {
        _flushCancellation?.Cancel();
        ClearPendingData();
    }

    private async Task SetDataAsync(DataTransfer dataTransfer, CancellationToken ct)
    {
        IClipboard? clipboard = await _platformContext
            .GetClipboardAsync(ct)
            .ConfigureAwait(false);

        if (clipboard is null)
        {
            return;
        }

        Task setDataTask = await Dispatcher.UIThread.InvokeAsync(
            () => SetDataOnUiThreadAsync(
                clipboard,
                dataTransfer,
                ct),
            DispatcherPriority.Normal,
            ct);
        await setDataTask;
    }

    private async Task SetDataOnUiThreadAsync(
        IClipboard clipboard,
        DataTransfer dataTransfer,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(clipboard);
        ct.ThrowIfCancellationRequested();
        await CancelOrWaitForActiveFlushAsync(ct);

        await clipboard.SetDataAsync(dataTransfer);
        _clipboardDataTransfer = dataTransfer;
        _hasPendingClipboardData = true;
    }

    private Task GetOrStartFlushTask(
        IClipboard clipboard,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(clipboard);
        ct.ThrowIfCancellationRequested();

        if (!_hasPendingClipboardData)
        {
            return Task.CompletedTask;
        }

        if (_flushTask is null)
        {
            CancellationTokenSource cancellation = new();
            _flushCancellation = cancellation;
            _flushTask = FlushPendingDataAsync(
                clipboard,
                cancellation);
        }

        Task flushTask = _flushTask;
        return ct.CanBeCanceled
            ? flushTask.WaitAsync(ct)
            : flushTask;
    }

    private async Task ReleasePendingDataOnUiThreadAsync(
        CancellationToken ct)
    {
        await CancelOrWaitForActiveFlushAsync(ct);
        ClearPendingData();
    }

    private async Task CancelOrWaitForActiveFlushAsync(CancellationToken ct)
    {
        Task? flushTask = _flushTask;

        if (flushTask is null)
        {
            return;
        }

        if (!_isFlushStarted)
        {
            _flushCancellation?.Cancel();
        }

        await flushTask.WaitAsync(ct);
    }

    private async Task FlushPendingDataAsync(
        IClipboard clipboard,
        CancellationTokenSource cancellation)
    {
        ArgumentNullException.ThrowIfNull(clipboard);

        try
        {
            Task uiFlushTask = await Dispatcher.UIThread.InvokeAsync(
                () => FlushOnUiThreadAsync(
                    clipboard,
                    cancellation.Token),
                DispatcherPriority.Background,
                cancellation.Token);
            await uiFlushTask;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            _logger.LogDebug("Pica clipboard persistence was canceled by a newer copy operation.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist Pica clipboard content.");
        }
        finally
        {
            if (object.ReferenceEquals(_flushCancellation, cancellation))
            {
                _flushCancellation = null;
                _flushTask = null;
                _isFlushStarted = false;
            }

            cancellation.Dispose();
        }
    }

    private async Task FlushOnUiThreadAsync(
        IClipboard clipboard,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(clipboard);
        ct.ThrowIfCancellationRequested();

        if (!_hasPendingClipboardData)
        {
            return;
        }

        IAsyncDataTransfer? currentData = await clipboard.TryGetInProcessDataAsync();
        ct.ThrowIfCancellationRequested();

        if (!object.ReferenceEquals(currentData, _clipboardDataTransfer))
        {
            ClearPendingData();
            return;
        }

        _isFlushStarted = true;
        await clipboard.FlushAsync();
        ClearPendingData();
    }

    private void ClearPendingData()
    {
        _clipboardDataTransfer = null;
        _hasPendingClipboardData = false;
    }
}
