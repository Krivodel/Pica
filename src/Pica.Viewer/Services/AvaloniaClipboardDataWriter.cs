using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;

namespace Pica.Viewer.Services;

internal sealed class AvaloniaClipboardDataWriter : IDisposable
{
    private readonly ILogger<AvaloniaClipboardDataWriter> _logger;
    private IClipboard? _clipboard;
    private IAsyncDataTransfer? _clipboardDataTransfer;
    private Task? _flushTask;
    private CancellationTokenSource? _flushCancellation;
    private bool _hasPendingClipboardData;
    private bool _isFlushStarted;

    public AvaloniaClipboardDataWriter(ILogger<AvaloniaClipboardDataWriter> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void Attach(IClipboard clipboard)
    {
        ArgumentNullException.ThrowIfNull(clipboard);

        _clipboard = clipboard;
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

    public Task FlushAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if ((_clipboard is null) || !_hasPendingClipboardData)
        {
            return Task.CompletedTask;
        }

        if (_flushTask is null)
        {
            CancellationTokenSource cancellation = new();
            _flushCancellation = cancellation;
            _flushTask = FlushPendingDataAsync(cancellation);
        }

        Task flushTask = _flushTask;
        return ct.CanBeCanceled
            ? flushTask.WaitAsync(ct)
            : flushTask;
    }

    public async Task ReleasePendingDataAsync(CancellationToken ct)
    {
        await CancelOrWaitForActiveFlushAsync(ct);
        ClearPendingData();
    }

    public void Dispose()
    {
        _flushCancellation?.Cancel();
        ClearPendingData();
    }

    private async Task SetDataAsync(DataTransfer dataTransfer, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await CancelOrWaitForActiveFlushAsync(ct);
        IClipboard? clipboard = _clipboard;

        if (clipboard is null)
        {
            return;
        }

        await clipboard.SetDataAsync(dataTransfer);
        _clipboardDataTransfer = dataTransfer;
        _hasPendingClipboardData = true;
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

    private async Task FlushPendingDataAsync(CancellationTokenSource cancellation)
    {
        try
        {
            Task uiFlushTask = await Dispatcher.UIThread.InvokeAsync(
                () => FlushOnUiThreadAsync(cancellation.Token),
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
            if (ReferenceEquals(_flushCancellation, cancellation))
            {
                _flushCancellation = null;
                _flushTask = null;
                _isFlushStarted = false;
            }

            cancellation.Dispose();
        }
    }

    private async Task FlushOnUiThreadAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        IClipboard? clipboard = _clipboard;

        if ((clipboard is null) || !_hasPendingClipboardData)
        {
            return;
        }

        IAsyncDataTransfer? currentData = await clipboard.TryGetInProcessDataAsync();
        ct.ThrowIfCancellationRequested();

        if (!ReferenceEquals(currentData, _clipboardDataTransfer))
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
