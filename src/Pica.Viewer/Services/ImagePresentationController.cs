using System.ComponentModel;

using Microsoft.Extensions.Logging;

using Avalonia;
using Avalonia.Media.Imaging;

using Pica.Protocol;

namespace Pica.Viewer.Services;

internal sealed class ImagePresentationController :
    IImagePresentationInfo,
    IImageLoadPresentationSink,
    IImageFrameSource,
    IDisposable
{
    public PicaImageItem? CurrentItem { get; private set; }
    public ImageDimensions SourceDimensions =>
        new(SourcePixelSize.Width, SourcePixelSize.Height);

    public event EventHandler? Changed;

    int IImageFrameSource.FrameCount =>
        _decodedImage?.FrameCount ?? 0;
    int IImageFrameSource.LoadedFrameCount =>
        _decodedImage?.LoadedFrameCount ?? 0;
    int IImageFrameSource.PlaybackBufferFrameCount =>
        _decodedImage?.PlaybackStartFrameCount ?? 0;
    TimeSpan IImageFrameSource.CurrentFrameDuration =>
        GetCurrentFrame()?.Duration ?? TimeSpan.Zero;
    ImageFramePresentationModes IImageFrameSource.FramePresentationMode =>
        _isContentGroupTransitioning
            ? ImageFramePresentationModes.None
            : _decodedImage?.FramePresentationMode
        ?? ImageFramePresentationModes.None;
    uint IImageFrameSource.AnimationIterations =>
        _decodedImage?.AnimationIterations ?? 0;
    bool IImageFrameSource.IsPlaybackStartBufferReady =>
        _decodedImage?.IsPlaybackStartBufferReady
        == true;
    bool IImageFrameSource.IsFullyDecoded =>
        _decodedImage?.IsFullyDecoded
        == true;
    bool IImageFrameSource.IsDecodingComplete =>
        _decodedImage?.IsDecodingComplete
        == true;

    event EventHandler? IImageFrameSource.FramesChanged
    {
        add => FramesChanged += value;
        remove => FramesChanged -= value;
    }
    event EventHandler? IImageFrameSource.FrameAvailabilityChanged
    {
        add => FrameAvailabilityChanged += value;
        remove => FrameAvailabilityChanged -= value;
    }

    internal Bitmap? DisplayedBitmap { get; private set; }
    internal Bitmap? SourceBitmap { get; private set; }
    internal PixelSize SourcePixelSize { get; private set; }
    internal ImageChannel? DisplayedChannel { get; private set; }
    internal bool IsFullResolutionReady { get; private set; }
    internal bool IsSourceBitmapDisplayed =>
        (SourceBitmap is not null)
        && object.ReferenceEquals(DisplayedBitmap, SourceBitmap);

    internal event EventHandler<ImageLoadTransitionEventArgs>? LoadTransitioned;
    internal event EventHandler? FramesChanged;
    internal event EventHandler? FrameAvailabilityChanged;

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
    private DecodedImageContent? _decodedContent;
    private DecodedImage? _decodedImage;
    private int _activeContentGroupIndex;
    private int _contentGroupLoadId;
    private int _contentNavigationTargetGroupIndex = -1;
    private int _contentNavigationTargetFrameIndex;
    private readonly Dictionary<int, int> _contentGroupFrameIndices = [];
    private long _channelLoadId;
    private OperationCancellation? _channelLoadCancellation;
    private Task? _activeChannelLoadTask;
    private TaskCompletionSource? _bitmapLeaseReleaseCompletion;
    private Task? _disposalTask;
    private bool _disposed;
    private bool _isContentGroupTransitioning;

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
        _session.ContentNavigationRequested +=
            OnContentNavigationRequested;
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
        DecodedImageContent content)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);
        ArgumentNullException.ThrowIfNull(content);
        Bitmap? previewBitmap = displayedPreview?.Bitmap;
        bool wasPreviewDisplayed = (previewBitmap is not null)
            && object.ReferenceEquals(DisplayedBitmap, previewBitmap);
        PixelSize previousPixelSize =
            previewBitmap?.PixelSize ?? new PixelSize();
        PicaImageItem displayedItem = item with { FilePath = fullPath };
        ReplaceFullResolutionContent(displayedItem, content);
        DecodedImage image = content.Groups[
            content.InitialGroupIndex].GetRequiredImage();
        Bitmap initialFrameBitmap = GetRequiredFrame(
            image,
            image.PreferredInitialFrameIndex).Bitmap;
        OnLoadTransitioned(
            new ImageLoadTransitionEventArgs(
                ImageLoadTransitionKind.FullResolutionApplied,
                wasPreviewDisplayed,
                previousPixelSize,
                initialFrameBitmap.PixelSize));
    }

    bool IImageFrameSource.IsFrameAvailable(int frameIndex)
    {
        return _decodedImage?.IsFrameAvailable(frameIndex)
            == true;
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
        _contentGroupLoadId++;
        _isContentGroupTransitioning = false;
        ClearContentNavigationTarget();
        _contentGroupFrameIndices.Clear();
        DecodedImage? image;
        DecodedImageContent? content;

        lock (_bitmapOwnershipSync)
        {
            IsFullResolutionReady = false;
            image = _decodedImage;
            content = _decodedContent;
        }

        image?.CancelDecoding();
        CancelContentLoading(content);
        _session.ClearContentGroups();
        _session.ClearFramePresentation();
        OnFramesChanged();
        CancelPendingChannelLoad();
    }

    internal void ReplaceFullResolutionBitmap(
        PicaImageItem item,
        Bitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(bitmap);
        ThrowIfDisposed();
        ReplaceFullResolutionImage(
            item,
            DecodedImage.CreateSingle(bitmap));
    }

    internal void ReplaceFullResolutionImage(
        PicaImageItem item,
        DecodedImage image)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(image);
        ReplaceFullResolutionContent(
            item,
            DecodedImageContent.CreateSingle(image));
    }

    internal void ReplaceFullResolutionContent(
        PicaImageItem item,
        DecodedImageContent content)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(content);
        ThrowIfDisposed();
        DecodedImage image = content.Groups[
            content.InitialGroupIndex].GetRequiredImage();
        _contentGroupLoadId++;
        _isContentGroupTransitioning = false;
        ClearContentNavigationTarget();
        _contentGroupFrameIndices.Clear();
        Task? activeChannelLoadTask = _activeChannelLoadTask;
        CancelPendingChannelLoad();
        Bitmap? previousChannelBitmap;
        Bitmap? previousSourceBitmap;
        DecodedImage? previousDecodedImage;
        DecodedImageContent? previousDecodedContent;
        image.SetStoredBitmapReleaseHandler(
            DisposeBitmapWhenUnused);
        image.SetPlaybackFrameIndex(
            image.PreferredInitialFrameIndex);
        DecodedImageFrame initialFrame = GetRequiredFrame(
            image,
            image.PreferredInitialFrameIndex);

        lock (_bitmapOwnershipSync)
        {
            previousChannelBitmap = _channelBitmap;
            previousSourceBitmap = SourceBitmap;
            previousDecodedImage = _decodedImage;
            previousDecodedContent = _decodedContent;

            if (previousDecodedImage is not null)
            {
                previousDecodedImage.FrameDecoded -=
                    OnDecodedImageFrameDecoded;
            }

            _channelBitmap = null;
            _decodedContent = content;
            _decodedImage = image;
            _activeContentGroupIndex = content.InitialGroupIndex;
            DisplayedChannel = null;
            SourceBitmap = initialFrame.Bitmap;
            DisplayedBitmap = initialFrame.Bitmap;
            CurrentItem = item;
            SourcePixelSize = initialFrame.Bitmap.PixelSize;
            IsFullResolutionReady = true;
        }

        image.FrameDecoded += OnDecodedImageFrameDecoded;
        DisposeBitmapWhenUnused(previousChannelBitmap);
        DisposeReplacedContent(
            previousDecodedContent,
            previousDecodedImage,
            previousSourceBitmap,
            activeChannelLoadTask);
        _session.SetContentGroups(
            content.Groups
                .Select(group => group.Definition)
                .ToList()
                .AsReadOnly(),
            content.InitialGroupIndex);
        _session.SetFramePresentation(
            image.FrameCount,
            image.FramePresentationMode,
            image.PreferredInitialFrameIndex,
            image.FrameNumbering,
            image.AnimationTimeline);
        _ = ObserveDecodedImageCompletionAsync(image);
        OnFramesChanged();
        OnDisplayedBitmapChanged();
        StartSelectedChannelLoad();
    }

    internal ImagePresentationBitmapLease? AcquireDisplayedBitmap(
        ImageChannel? expectedChannel)
    {
        ThrowIfDisposed();

        lock (_bitmapOwnershipSync)
        {
            if (!IsDisplayedBitmapReadyCore(expectedChannel))
            {
                return null;
            }

            return AcquireDisplayedBitmapCore();
        }
    }

    internal ImagePresentationBitmapLease? AcquireDisplayedBitmapForRendering()
    {
        ThrowIfDisposed();

        lock (_bitmapOwnershipSync)
        {
            return AcquireDisplayedBitmapCore();
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

    private static DecodedImageFrame GetRequiredFrame(
        DecodedImage image,
        int frameIndex)
    {
        return image.GetFrame(frameIndex)
            ?? throw new InvalidOperationException(
                $"Decoded image frame {frameIndex} is not available.");
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
            _session.ContentNavigationRequested -=
                OnContentNavigationRequested;
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
        DecodedImage? decodedImage;
        DecodedImageContent? decodedContent;

        lock (_bitmapOwnershipSync)
        {
            channelBitmap = _channelBitmap;
            sourceBitmap = SourceBitmap;
            decodedImage = _decodedImage;
            decodedContent = _decodedContent;

            if (decodedImage is not null)
            {
                decodedImage.FrameDecoded -=
                    OnDecodedImageFrameDecoded;
            }

            _channelBitmap = null;
            _decodedContent = null;
            _decodedImage = null;
            DisplayedBitmap = null;
            SourceBitmap = null;
            CurrentItem = null;
            SourcePixelSize = new PixelSize();
            DisplayedChannel = null;
            IsFullResolutionReady = false;
        }

        OnDisplayedBitmapChanged();
        DisposeBitmapWhenUnused(channelBitmap);

        if (decodedContent is not null)
        {
            await DisposeDecodedContentForCleanupAsync(
                decodedContent,
                decodedImage,
                activeChannelLoadTask)
                .ConfigureAwait(false);
        }
        else if (decodedImage is not null)
        {
            await DisposeDecodedImageForCleanupAsync(
                decodedImage,
                activeChannelLoadTask)
                .ConfigureAwait(false);
        }
        else if (sourceBitmap is not null)
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
        DecodedImage? previousDecodedImage;
        DecodedImageContent? previousDecodedContent;

        lock (_bitmapOwnershipSync)
        {
            previousChannelBitmap = _channelBitmap;
            previousSourceBitmap = SourceBitmap;
            previousDecodedImage = _decodedImage;
            previousDecodedContent = _decodedContent;

            if (previousDecodedImage is not null)
            {
                previousDecodedImage.FrameDecoded -=
                    OnDecodedImageFrameDecoded;
            }

            _channelBitmap = null;
            _decodedContent = null;
            _decodedImage = null;
            DisplayedChannel = null;
            SourceBitmap = bitmap;
            DisplayedBitmap = bitmap;
            CurrentItem = item;
            SourcePixelSize = sourcePixelSize;
            IsFullResolutionReady = isFullResolutionReady;
        }

        DisposeBitmapWhenUnused(previousChannelBitmap);

        DisposeReplacedContent(
            previousDecodedContent,
            previousDecodedImage,
            previousSourceBitmap,
            activeChannelLoadTask);

        OnDisplayedBitmapChanged();
    }

    private DecodedImageFrame? GetCurrentFrame()
    {
        DecodedImage? image = _decodedImage;
        int frameIndex = _session.SelectedFrameIndex;

        return image?.GetFrame(frameIndex);
    }

    private void ShowSelectedFrame()
    {
        if (!IsFullResolutionReady)
        {
            return;
        }

        DecodedImage? image = _decodedImage;

        if (image is null)
        {
            return;
        }

        image.SetPlaybackFrameIndex(
            _session.SelectedFrameIndex);
        DecodedImageFrame? frame = GetCurrentFrame();

        if (frame is null)
        {
            return;
        }

        CancelPendingChannelLoad();
        Bitmap? channelBitmap;

        lock (_bitmapOwnershipSync)
        {
            channelBitmap = _channelBitmap;
            _channelBitmap = null;
            DisplayedChannel = null;
            SourceBitmap = frame.Bitmap;
            DisplayedBitmap = frame.Bitmap;
            SourcePixelSize = frame.Bitmap.PixelSize;
        }

        DisposeBitmapWhenUnused(channelBitmap);
        OnDisplayedBitmapChanged();
        StartSelectedChannelLoad();
    }

    private void StartContentGroupSelection()
    {
        DecodedImageContent? content = _decodedContent;
        int requestedGroupIndex =
            _session.SelectedContentGroupIndex;

        if ((content is null)
            || (requestedGroupIndex < 0)
            || (requestedGroupIndex >= content.Groups.Count))
        {
            return;
        }

        if (requestedGroupIndex != _contentNavigationTargetGroupIndex)
        {
            ClearContentNavigationTarget();
        }

        if ((requestedGroupIndex == _activeContentGroupIndex)
            && !_isContentGroupTransitioning)
        {
            return;
        }

        int previousGroupIndex = _activeContentGroupIndex;
        _contentGroupFrameIndices[previousGroupIndex] =
            _session.SelectedFrameIndex;
        int loadId = ++_contentGroupLoadId;
        _isContentGroupTransitioning = true;
        OnFramesChanged();
        DecodedImageContentGroup group =
            content.Groups[requestedGroupIndex];

        if (group.IsLoaded)
        {
            ApplyContentGroup(
                content,
                requestedGroupIndex,
                loadId);
            return;
        }

        _session.SetContentGroupLoading(true);
        _ = LoadContentGroupAsync(
            content,
            requestedGroupIndex,
            previousGroupIndex,
            loadId);
    }

    private async Task LoadContentGroupAsync(
        DecodedImageContent content,
        int groupIndex,
        int previousGroupIndex,
        int loadId)
    {
        try
        {
            await content.Groups[groupIndex]
                .LoadAsync(CancellationToken.None)
                .ConfigureAwait(false);
            await _uiDispatcher.InvokeAsync(
                () => ApplyContentGroup(
                    content,
                    groupIndex,
                    loadId),
                CancellationToken.None).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await CompleteFailedContentGroupSelectionAsync(
                content,
                groupIndex,
                previousGroupIndex,
                loadId,
                null).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await CompleteFailedContentGroupSelectionAsync(
                content,
                groupIndex,
                previousGroupIndex,
                loadId,
                ex).ConfigureAwait(false);
        }
    }

    private async Task CompleteFailedContentGroupSelectionAsync(
        DecodedImageContent content,
        int groupIndex,
        int previousGroupIndex,
        int loadId,
        Exception? exception)
    {
        if (exception is not null)
        {
            _logger.LogError(
                exception,
                "Failed to load image content group {GroupKind}.",
                content.Groups[groupIndex].Definition.Kind);
        }

        await _uiDispatcher.InvokeAsync(
            () =>
            {
                if (!CanApplyContentGroup(
                    content,
                    groupIndex,
                    loadId))
                {
                    return;
                }

                _isContentGroupTransitioning = false;
                _session.SetContentGroupLoading(false);
                ClearContentNavigationTarget(groupIndex);
                _session.RestoreContentGroupSelection(
                    previousGroupIndex);
                OnFramesChanged();
            },
            CancellationToken.None).ConfigureAwait(false);
    }

    private void ApplyContentGroup(
        DecodedImageContent content,
        int groupIndex,
        int loadId)
    {
        if (!CanApplyContentGroup(content, groupIndex, loadId))
        {
            return;
        }

        DecodedImageContentGroup group = content.Groups[groupIndex];
        DecodedImage image = group.GetRequiredImage();
        int frameIndex = GetContentGroupFrameIndex(
            group,
            groupIndex,
            image);
        image.SetStoredBitmapReleaseHandler(
            DisposeBitmapWhenUnused);
        image.SetPlaybackFrameIndex(frameIndex);
        DecodedImageFrame initialFrame = GetRequiredFrame(
            image,
            frameIndex);
        PixelSize previousPixelSize = SourcePixelSize;
        CancelPendingChannelLoad();
        Bitmap? previousChannelBitmap;
        DecodedImage? previousImage;

        lock (_bitmapOwnershipSync)
        {
            previousChannelBitmap = _channelBitmap;
            previousImage = _decodedImage;

            if (previousImage is not null)
            {
                previousImage.FrameDecoded -=
                    OnDecodedImageFrameDecoded;
            }

            _channelBitmap = null;
            _decodedImage = image;
            _activeContentGroupIndex = groupIndex;
            DisplayedChannel = null;
            SourceBitmap = initialFrame.Bitmap;
            DisplayedBitmap = initialFrame.Bitmap;
            SourcePixelSize = initialFrame.Bitmap.PixelSize;
        }

        image.FrameDecoded += OnDecodedImageFrameDecoded;
        DisposeBitmapWhenUnused(previousChannelBitmap);
        _isContentGroupTransitioning = false;
        _session.SetContentGroupLoading(false);
        ClearContentNavigationTarget(groupIndex);
        _session.SetFramePresentation(
            image.FrameCount,
            image.FramePresentationMode,
            frameIndex,
            image.FrameNumbering,
            image.AnimationTimeline);
        _ = ObserveDecodedImageCompletionAsync(image);
        OnFramesChanged();
        OnDisplayedBitmapChanged();
        OnLoadTransitioned(
            new ImageLoadTransitionEventArgs(
                ImageLoadTransitionKind.ContentGroupApplied,
                false,
                previousPixelSize,
                initialFrame.Bitmap.PixelSize));
        StartSelectedChannelLoad();
    }

    private bool CanApplyContentGroup(
        DecodedImageContent content,
        int groupIndex,
        int loadId)
    {
        return !_disposed
            && object.ReferenceEquals(content, _decodedContent)
            && (groupIndex == _session.SelectedContentGroupIndex)
            && (loadId == _contentGroupLoadId);
    }

    private int GetContentGroupFrameIndex(
        DecodedImageContentGroup group,
        int groupIndex,
        DecodedImage image)
    {
        if (group.Definition.Kind == ImageContentGroupKind.Animation)
        {
            return 0;
        }

        if (groupIndex == _contentNavigationTargetGroupIndex)
        {
            return _contentNavigationTargetFrameIndex;
        }

        return _contentGroupFrameIndices.GetValueOrDefault(
            groupIndex,
            image.PreferredInitialFrameIndex);
    }

    private void ClearContentNavigationTarget(int groupIndex)
    {
        if (groupIndex == _contentNavigationTargetGroupIndex)
        {
            ClearContentNavigationTarget();
        }
    }

    private void ClearContentNavigationTarget()
    {
        _contentNavigationTargetGroupIndex = -1;
        _contentNavigationTargetFrameIndex = 0;
    }

    private void DisposeReplacedSource(
        DecodedImage? decodedImage,
        Bitmap? sourceBitmap,
        Task? activeTask)
    {
        if (decodedImage is not null)
        {
            DisposeDecodedImageAfterTask(
                decodedImage,
                activeTask);
            return;
        }

        if (sourceBitmap is not null)
        {
            DisposeBitmapAfterTask(sourceBitmap, activeTask);
        }
    }

    private static void CancelContentLoading(
        DecodedImageContent? content)
    {
        if (content is null)
        {
            return;
        }

        foreach (DecodedImageContentGroup group in content.Groups)
        {
            group.CancelLoading();
        }
    }

    private void DisposeReplacedContent(
        DecodedImageContent? content,
        DecodedImage? activeImage,
        Bitmap? activeBitmap,
        Task? activeTask)
    {
        if (content is null)
        {
            DisposeReplacedSource(
                activeImage,
                activeBitmap,
                activeTask);
            return;
        }

        foreach (DecodedImageContentGroup group in content.Groups)
        {
            IReadOnlyList<DecodedImage> images =
                group.StopAndGetLoadedImages();

            foreach (DecodedImage image in images)
            {
                bool isActiveImage = object.ReferenceEquals(
                    image,
                    activeImage);
                DisposeDecodedImageAfterTask(
                    image,
                    isActiveImage ? activeTask : null);
            }

            ObserveStoppedGroupLoading(group);
        }
    }

    private void ObserveStoppedGroupLoading(
        DecodedImageContentGroup group)
    {
        Task? loadingCompletion = group.GetLoadingCompletion();

        if ((loadingCompletion is not null)
            && !loadingCompletion.IsCompletedSuccessfully)
        {
            _ = ObserveStoppedGroupLoadingAsync(loadingCompletion);
        }
    }

    private async Task ObserveStoppedGroupLoadingAsync(Task loadingTask)
    {
        try
        {
            await loadingTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "A deferred image content group failed while its container was being released.");
        }
    }

    private void DisposeDecodedImageAfterTask(
        DecodedImage image,
        Task? activeTask)
    {
        image.CancelDecoding();
        _ = DisposeDecodedImageAfterTaskAsync(
            image,
            activeTask);
    }

    private async Task DisposeDecodedImageAfterTaskAsync(
        DecodedImage image,
        Task? activeTask)
    {
        try
        {
            await image.DecodingCompletion.ConfigureAwait(false);

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
                        "A channel operation failed while releasing a replaced Pica image.");
                }
            }

            image.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to release a replaced progressively decoded Pica image.");
        }
    }

    private async Task DisposeDecodedImageForCleanupAsync(
        DecodedImage image,
        Task? activeTask)
    {
        image.CancelDecoding();
        await image.DecodingCompletion.ConfigureAwait(false);

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

        image.Dispose();
    }

    private async Task DisposeDecodedContentForCleanupAsync(
        DecodedImageContent content,
        DecodedImage? activeImage,
        Task? activeTask)
    {
        foreach (DecodedImageContentGroup group in content.Groups)
        {
            IReadOnlyList<DecodedImage> images =
                group.StopAndGetLoadedImages();

            foreach (DecodedImage image in images)
            {
                await DisposeDecodedImageForCleanupAsync(
                    image,
                    object.ReferenceEquals(image, activeImage)
                        ? activeTask
                        : null).ConfigureAwait(false);
            }

            Task? loadingCompletion = group.GetLoadingCompletion();

            if (loadingCompletion is not null)
            {
                try
                {
                    await loadingCompletion.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "A deferred image content group failed while closing the Pica viewer.");
                }
            }
        }
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

    private ImagePresentationBitmapLease? AcquireDisplayedBitmapCore()
    {
        Bitmap? bitmap = DisplayedBitmap;

        if (bitmap is null)
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

    private async Task ObserveDecodedImageCompletionAsync(
        DecodedImage image)
    {
        Exception? decodingException =
            await image.DecodingCompletion.ConfigureAwait(false);

        if (decodingException is not null)
        {
            _logger.LogError(
                decodingException,
                "Failed to decode all frames of a Pica image.");
        }

        bool isCurrentImage;

        lock (_bitmapOwnershipSync)
        {
            isCurrentImage = object.ReferenceEquals(
                image,
                _decodedImage);
        }

        if (isCurrentImage)
        {
            OnFrameAvailabilityChanged();
        }
    }

    private async Task ApplyDecodedFrameAvailabilityAsync(
        DecodedImage image)
    {
        try
        {
            await _uiDispatcher.InvokeAsync(
                () =>
                {
                    if (_disposed
                        || !object.ReferenceEquals(
                            image,
                            _decodedImage))
                    {
                        return;
                    }

                    DecodedImageFrame? selectedFrame =
                        image.GetFrame(
                            _session.SelectedFrameIndex);

                    if ((selectedFrame is not null)
                        && !object.ReferenceEquals(
                            selectedFrame.Bitmap,
                            SourceBitmap))
                    {
                        ShowSelectedFrame();
                    }
                },
                CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to apply a progressively decoded Pica image frame.");
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

    private void OnFramesChanged()
    {
        FramesChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnFrameAvailabilityChanged()
    {
        FrameAvailabilityChanged?.Invoke(
            this,
            EventArgs.Empty);
    }

    private void OnLoadTransitioned(ImageLoadTransitionEventArgs e)
    {
        LoadTransitioned?.Invoke(this, e);
    }

    private void OnDecodedImageFrameDecoded(
        object? sender,
        DecodedImageFrameDecodedEventArgs e)
    {
        if ((sender is not DecodedImage image)
            || !object.ReferenceEquals(
                image,
                _decodedImage))
        {
            return;
        }

        OnFrameAvailabilityChanged();

        if (e.FrameIndex
            == _session.SelectedFrameIndex)
        {
            _ = ApplyDecodedFrameAvailabilityAsync(image);
        }
    }

    private void OnSessionPropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        _ = sender;

        if (string.Equals(
            e.PropertyName,
            nameof(ImageViewerSession.SelectedContentGroupIndex),
            StringComparison.Ordinal))
        {
            StartContentGroupSelection();
            return;
        }

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
            nameof(ImageViewerSession.SelectedFrameIndex),
            StringComparison.Ordinal))
        {
            ShowSelectedFrame();
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

    private void OnContentNavigationRequested(
        object? sender,
        ImageContentNavigationRequestedEventArgs e)
    {
        _ = sender;
        _contentGroupFrameIndices[e.GroupIndex] = e.FrameIndex;

        if ((e.GroupIndex == _activeContentGroupIndex)
            && !_isContentGroupTransitioning)
        {
            ClearContentNavigationTarget();
            return;
        }

        _contentNavigationTargetGroupIndex = e.GroupIndex;
        _contentNavigationTargetFrameIndex = e.FrameIndex;
    }
}
