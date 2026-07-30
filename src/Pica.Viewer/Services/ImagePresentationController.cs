using System.ComponentModel;

using Microsoft.Extensions.Logging;

using Avalonia;
using Avalonia.Media.Imaging;

using Pica.Protocol;

namespace Pica.Viewer.Services;

internal sealed class ImagePresentationController :
    IImagePresentationInfo,
    IImageLoadPresentationSink,
    IDisposable
{
    public PicaImageItem? CurrentItem { get; private set; }
    public ImageDimensions SourceDimensions =>
        new(SourcePixelSize.Width, SourcePixelSize.Height);

    public event EventHandler? Changed;

    internal Bitmap? DisplayedBitmap { get; private set; }
    internal Bitmap? SourceBitmap { get; private set; }
    internal PixelSize SourcePixelSize { get; private set; }
    internal ImageChannel? DisplayedChannel { get; private set; }
    internal bool IsFullResolutionReady { get; private set; }
    internal bool IsSourceBitmapDisplayed =>
        (SourceBitmap is not null)
        && object.ReferenceEquals(DisplayedBitmap, SourceBitmap);

    internal event EventHandler<ImageLoadTransitionEventArgs>? LoadTransitioned;

    private readonly ImageViewerSession _session;
    private readonly IImageChannelBitmapLoader _channelBitmapLoader;
    private readonly IViewerUiDispatcher _uiDispatcher;
    private readonly ILogger<ImagePresentationController> _logger;
    private readonly object _bitmapOwnershipSync = new();
    private readonly object _disposalSync = new();
    private readonly Dictionary<Bitmap, int> _bitmapUseCounts =
        new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<Bitmap> _pendingBitmapDisposals =
        new(ReferenceEqualityComparer.Instance);
    private Bitmap? _channelBitmap;
    private long _channelLoadId;
    private OperationCancellation? _channelLoadCancellation;
    private Task? _activeChannelLoadTask;
    private TaskCompletionSource? _bitmapLeaseReleaseCompletion;
    private Task? _disposalTask;
    private bool _disposed;

    internal ImagePresentationController(
        ImageViewerSession session,
        IImageChannelBitmapLoader channelBitmapLoader,
        IViewerUiDispatcher uiDispatcher,
        ILogger<ImagePresentationController> logger)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _channelBitmapLoader = channelBitmapLoader
            ?? throw new ArgumentNullException(nameof(channelBitmapLoader));
        _uiDispatcher = uiDispatcher
            ?? throw new ArgumentNullException(nameof(uiDispatcher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _session.PropertyChanged += OnSessionPropertyChanged;
    }

    public void Dispose()
    {
        Task disposalTask = BeginDisposal();

        if (!disposalTask.IsCompletedSuccessfully)
        {
            _ = ObserveDisposalAsync(disposalTask);
        }
    }

    void IImageLoadPresentationSink.BeginImageLoad(PicaImageItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        BeginImageLoad();
        OnLoadTransitioned(
            new ImageLoadTransitionEventArgs(
                ImageLoadTransitionKind.Started,
                false,
                new PixelSize(),
                new PixelSize()));
    }

    void IImageLoadPresentationSink.ApplyPreview(
        PicaImageItem item,
        string fullPath,
        DecodedImagePreview preview)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);
        ArgumentNullException.ThrowIfNull(preview);
        PicaImageItem displayedItem = item with { FilePath = fullPath };
        ReplacePreviewBitmap(
            displayedItem,
            preview.Bitmap,
            preview.SourcePixelSize);
        OnLoadTransitioned(
            new ImageLoadTransitionEventArgs(
                ImageLoadTransitionKind.PreviewApplied,
                false,
                new PixelSize(),
                preview.Bitmap.PixelSize));
    }

    void IImageLoadPresentationSink.ApplyFullResolution(
        PicaImageItem item,
        string fullPath,
        DecodedImagePreview? displayedPreview,
        Bitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);
        ArgumentNullException.ThrowIfNull(bitmap);
        Bitmap? previewBitmap = displayedPreview?.Bitmap;
        bool wasPreviewDisplayed = (previewBitmap is not null)
            && object.ReferenceEquals(DisplayedBitmap, previewBitmap);
        PixelSize previousPixelSize =
            previewBitmap?.PixelSize ?? new PixelSize();
        PicaImageItem displayedItem = item with { FilePath = fullPath };
        ReplaceFullResolutionBitmap(displayedItem, bitmap);
        OnLoadTransitioned(
            new ImageLoadTransitionEventArgs(
                ImageLoadTransitionKind.FullResolutionApplied,
                wasPreviewDisplayed,
                previousPixelSize,
                bitmap.PixelSize));
    }

    internal void ReplacePreviewBitmap(
        PicaImageItem item,
        Bitmap bitmap,
        PixelSize sourcePixelSize)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(bitmap);
        ThrowIfDisposed();
        ReplaceSourceBitmap(
            item,
            bitmap,
            sourcePixelSize,
            false);
    }

    internal void BeginImageLoad()
    {
        ThrowIfDisposed();

        lock (_bitmapOwnershipSync)
        {
            IsFullResolutionReady = false;
        }

        CancelPendingChannelLoad();
    }

    internal void ReplaceFullResolutionBitmap(
        PicaImageItem item,
        Bitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(bitmap);
        ThrowIfDisposed();
        ReplaceSourceBitmap(
            item,
            bitmap,
            bitmap.PixelSize,
            true);
        StartSelectedChannelLoad();
    }

    internal ImagePresentationBitmapLease? AcquireDisplayedBitmap(
        ImageChannel? expectedChannel)
    {
        ThrowIfDisposed();

        lock (_bitmapOwnershipSync)
        {
            Bitmap? bitmap = DisplayedBitmap;

            if (!IsDisplayedBitmapReadyCore(expectedChannel)
                || (bitmap is null))
            {
                return null;
            }

            _bitmapUseCounts.TryGetValue(
                bitmap,
                out int useCount);
            _bitmapUseCounts[bitmap] = useCount + 1;

            return new ImagePresentationBitmapLease(
                bitmap,
                ReleaseBitmap);
        }
    }

    internal bool IsDisplayedBitmapReady(
        ImageChannel? expectedChannel)
    {
        lock (_bitmapOwnershipSync)
        {
            return IsDisplayedBitmapReadyCore(expectedChannel);
        }
    }

    internal async Task WaitForSelectedChannelAsync(CancellationToken ct)
    {
        ThrowIfDisposed();
        Task? channelLoadTask = await _uiDispatcher
            .InvokeAsync(
                GetOrStartSelectedChannelLoad,
                ct)
            .ConfigureAwait(false);

        if (channelLoadTask is not null)
        {
            await channelLoadTask
                .WaitAsync(ct)
                .ConfigureAwait(false);
        }
    }

    internal async Task DisposeAsync(CancellationToken ct)
    {
        Task disposalTask = BeginDisposal();

        await disposalTask
            .WaitAsync(ct)
            .ConfigureAwait(false);
    }

    private Task BeginDisposal()
    {
        lock (_disposalSync)
        {
            if (_disposalTask is not null)
            {
                return _disposalTask;
            }

            _session.PropertyChanged -= OnSessionPropertyChanged;
            _disposed = true;
            _disposalTask = ReleaseResourcesAsync();

            return _disposalTask;
        }
    }

    private async Task ReleaseResourcesAsync()
    {
        Task? activeChannelLoadTask = _activeChannelLoadTask;
        CancelPendingChannelLoad();
        Bitmap? channelBitmap;
        Bitmap? sourceBitmap;

        lock (_bitmapOwnershipSync)
        {
            channelBitmap = _channelBitmap;
            sourceBitmap = SourceBitmap;
            _channelBitmap = null;
            DisplayedBitmap = null;
            SourceBitmap = null;
            CurrentItem = null;
            SourcePixelSize = new PixelSize();
            DisplayedChannel = null;
            IsFullResolutionReady = false;
        }

        OnDisplayedBitmapChanged();
        DisposeBitmapWhenUnused(channelBitmap);

        if (sourceBitmap is not null)
        {
            await DisposeBitmapForCleanupAsync(
                sourceBitmap,
                activeChannelLoadTask)
                .ConfigureAwait(false);
        }

        await WaitForBitmapLeasesAsync().ConfigureAwait(false);
    }

    private bool IsDisplayedBitmapReadyCore(
        ImageChannel? expectedChannel)
    {
        return IsFullResolutionReady
            && (DisplayedBitmap is not null)
            && object.Equals(
                DisplayedChannel,
                expectedChannel);
    }

    private Task? GetOrStartSelectedChannelLoad()
    {
        ThrowIfDisposed();

        if (!_session.IsChannelModeActive
            || IsDisplayedBitmapReady(
                _session.SelectedChannel))
        {
            return null;
        }

        if (_activeChannelLoadTask is null)
        {
            StartSelectedChannelLoad();
        }

        return _activeChannelLoadTask;
    }

    private void DisposeBitmapAfterTask(
        Bitmap bitmap,
        Task? activeTask)
    {
        if ((activeTask is null) || activeTask.IsCompleted)
        {
            DisposeBitmapWhenUnused(bitmap);
            return;
        }

        _ = DisposeBitmapAfterTaskAsync(bitmap, activeTask);
    }

    private async Task DisposeBitmapAfterTaskAsync(
        Bitmap bitmap,
        Task activeTask)
    {
        try
        {
            await activeTask.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "A channel operation failed while releasing a replaced Pica bitmap.");
        }

        try
        {
            DisposeBitmapWhenUnused(bitmap);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to release a replaced Pica bitmap.");
        }
    }

    private async Task DisposeBitmapForCleanupAsync(
        Bitmap bitmap,
        Task? activeTask)
    {
        if (activeTask is not null)
        {
            try
            {
                await activeTask.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "A channel operation failed while closing the Pica viewer.");
            }
        }

        DisposeBitmapWhenUnused(bitmap);
    }

    private void StartSelectedChannelLoad()
    {
        ImageChannel? channel = _session.SelectedChannel;
        Bitmap? sourceBitmap = SourceBitmap;
        PicaImageItem? item = CurrentItem;

        if (!IsFullResolutionReady
            || (channel is null)
            || (sourceBitmap is null)
            || (item is null))
        {
            return;
        }

        CancelPendingChannelLoad();
        OperationCancellation cancellation = new();
        long loadId = ++_channelLoadId;
        _channelLoadCancellation = cancellation;
        _activeChannelLoadTask = LoadSelectedChannelAsync(
            item,
            sourceBitmap,
            channel,
            loadId,
            cancellation);
    }

    private async Task LoadSelectedChannelAsync(
        PicaImageItem item,
        Bitmap sourceBitmap,
        ImageChannel channel,
        long loadId,
        OperationCancellation cancellation)
    {
        Bitmap? channelBitmap = null;
        CancellationToken ct = cancellation.Token;

        try
        {
            if (!_session.IsChannelAvailabilityKnown)
            {
                bool hasAlpha = await _channelBitmapLoader
                    .ReadHasAlphaAsync(item.FilePath, ct)
                    .ConfigureAwait(false);
                bool availabilityApplied = await _uiDispatcher.InvokeAsync(
                    () => ApplyChannelAvailability(
                        sourceBitmap,
                        channel,
                        loadId,
                        hasAlpha,
                        ct),
                    ct).ConfigureAwait(false);

                if (!availabilityApplied)
                {
                    return;
                }
            }

            channelBitmap = await _channelBitmapLoader.LoadAsync(
                sourceBitmap,
                channel,
                ct).ConfigureAwait(false);
            bool bitmapApplied = await _uiDispatcher.InvokeAsync(
                () => TryApplyChannelBitmap(
                    sourceBitmap,
                    channelBitmap,
                    channel,
                    loadId,
                    ct),
                ct).ConfigureAwait(false);

            if (!bitmapApplied)
            {
                return;
            }

            channelBitmap = null;
            _logger.LogInformation(
                "Loaded channel {Channel} for Pica image {ItemId}",
                channel.Code,
                item.Id);
        }
        catch (OperationCanceledException ex) when (ct.IsCancellationRequested)
        {
            _logger.LogDebug(
                ex,
                "Cancelled channel {Channel} load for Pica image {ItemId}",
                channel.Code,
                item.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to load channel {Channel} for Pica image {ItemId}.",
                channel.Code,
                item.Id);
        }
        finally
        {
            channelBitmap?.Dispose();

            try
            {
                await _uiDispatcher.InvokeAsync(
                    () => CompleteChannelLoad(cancellation),
                    CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to complete Pica channel loading on the UI thread.");
            }
            finally
            {
                cancellation.Complete();
            }
        }
    }

    private bool ApplyChannelAvailability(
        Bitmap sourceBitmap,
        ImageChannel channel,
        long loadId,
        bool hasAlpha,
        CancellationToken ct)
    {
        if (!CanApplyChannelLoad(sourceBitmap, channel, loadId, ct))
        {
            return false;
        }

        _session.SetHasAlpha(hasAlpha);

        return true;
    }

    private bool TryApplyChannelBitmap(
        Bitmap sourceBitmap,
        Bitmap channelBitmap,
        ImageChannel channel,
        long loadId,
        CancellationToken ct)
    {
        if (!CanApplyChannelLoad(sourceBitmap, channel, loadId, ct))
        {
            return false;
        }

        ApplyChannelBitmap(channelBitmap, channel);

        return true;
    }

    private void CompleteChannelLoad(OperationCancellation cancellation)
    {
        if (object.ReferenceEquals(_channelLoadCancellation, cancellation))
        {
            _channelLoadCancellation = null;
            _activeChannelLoadTask = null;
        }
    }

    private bool CanApplyChannelLoad(
        Bitmap sourceBitmap,
        ImageChannel channel,
        long loadId,
        CancellationToken ct)
    {
        lock (_bitmapOwnershipSync)
        {
            return !ct.IsCancellationRequested
                && (loadId == _channelLoadId)
                && _session.IsChannelModeActive
                && object.ReferenceEquals(sourceBitmap, SourceBitmap)
                && object.Equals(channel, _session.SelectedChannel);
        }
    }

    private void ApplyChannelBitmap(
        Bitmap channelBitmap,
        ImageChannel channel)
    {
        Bitmap? previousChannelBitmap;

        lock (_bitmapOwnershipSync)
        {
            previousChannelBitmap = _channelBitmap;
            _channelBitmap = channelBitmap;
            DisplayedChannel = channel;
            DisplayedBitmap = channelBitmap;
        }

        DisposeBitmapWhenUnused(previousChannelBitmap);
        OnDisplayedBitmapChanged();
    }

    private void ShowSourceBitmap()
    {
        Bitmap? channelBitmap;

        lock (_bitmapOwnershipSync)
        {
            channelBitmap = _channelBitmap;
            _channelBitmap = null;
            DisplayedChannel = null;
            DisplayedBitmap = SourceBitmap;
        }

        DisposeBitmapWhenUnused(channelBitmap);
        OnDisplayedBitmapChanged();
    }

    private void ReplaceSourceBitmap(
        PicaImageItem item,
        Bitmap bitmap,
        PixelSize sourcePixelSize,
        bool isFullResolutionReady)
    {
        Task? activeChannelLoadTask = _activeChannelLoadTask;
        CancelPendingChannelLoad();
        Bitmap? previousChannelBitmap;
        Bitmap? previousSourceBitmap;

        lock (_bitmapOwnershipSync)
        {
            previousChannelBitmap = _channelBitmap;
            previousSourceBitmap = SourceBitmap;
            _channelBitmap = null;
            DisplayedChannel = null;
            SourceBitmap = bitmap;
            DisplayedBitmap = bitmap;
            CurrentItem = item;
            SourcePixelSize = sourcePixelSize;
            IsFullResolutionReady = isFullResolutionReady;
        }

        DisposeBitmapWhenUnused(previousChannelBitmap);

        if (previousSourceBitmap is not null)
        {
            DisposeBitmapAfterTask(
                previousSourceBitmap,
                activeChannelLoadTask);
        }

        OnDisplayedBitmapChanged();
    }

    private void DisposeBitmapWhenUnused(Bitmap? bitmap)
    {
        if (bitmap is null)
        {
            return;
        }

        lock (_bitmapOwnershipSync)
        {
            if (_bitmapUseCounts.ContainsKey(bitmap))
            {
                _pendingBitmapDisposals.Add(bitmap);
                return;
            }
        }

        bitmap.Dispose();
    }

    private void ReleaseBitmap(Bitmap bitmap)
    {
        bool shouldDispose = false;
        TaskCompletionSource? leaseReleaseCompletion = null;

        lock (_bitmapOwnershipSync)
        {
            if (!_bitmapUseCounts.TryGetValue(
                    bitmap,
                    out int useCount))
            {
                return;
            }

            if (useCount > 1)
            {
                _bitmapUseCounts[bitmap] = useCount - 1;
                return;
            }

            _bitmapUseCounts.Remove(bitmap);
            shouldDispose =
                _pendingBitmapDisposals.Remove(bitmap);

            if (_bitmapUseCounts.Count == 0)
            {
                leaseReleaseCompletion =
                    _bitmapLeaseReleaseCompletion;
                _bitmapLeaseReleaseCompletion = null;
            }
        }

        try
        {
            if (shouldDispose)
            {
                bitmap.Dispose();
            }
        }
        finally
        {
            leaseReleaseCompletion?.TrySetResult();
        }
    }

    private Task WaitForBitmapLeasesAsync()
    {
        lock (_bitmapOwnershipSync)
        {
            if (_bitmapUseCounts.Count == 0)
            {
                return Task.CompletedTask;
            }

            _bitmapLeaseReleaseCompletion ??= new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

            return _bitmapLeaseReleaseCompletion.Task;
        }
    }

    private async Task ObserveDisposalAsync(Task disposalTask)
    {
        try
        {
            await disposalTask.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to finish disposing Pica image presentation resources.");
        }
    }

    private void CancelPendingChannelLoad()
    {
        _channelLoadId++;
        OperationCancellation? cancellation = _channelLoadCancellation;
        _channelLoadCancellation = null;
        _activeChannelLoadTask = null;
        cancellation?.Cancel();
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private void OnDisplayedBitmapChanged()
    {
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnLoadTransitioned(ImageLoadTransitionEventArgs e)
    {
        LoadTransitioned?.Invoke(this, e);
    }

    private void OnSessionPropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        _ = sender;

        if (string.Equals(
            e.PropertyName,
            nameof(ImageViewerSession.ImageMode),
            StringComparison.Ordinal))
        {
            if (_session.IsChannelModeActive)
            {
                OnDisplayedBitmapChanged();
                StartSelectedChannelLoad();
                return;
            }

            CancelPendingChannelLoad();
            ShowSourceBitmap();
            return;
        }

        if (string.Equals(
            e.PropertyName,
            nameof(ImageViewerSession.SelectedChannel),
            StringComparison.Ordinal)
            && _session.IsChannelModeActive)
        {
            OnDisplayedBitmapChanged();
            StartSelectedChannelLoad();
        }
    }
}
