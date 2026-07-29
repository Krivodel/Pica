using System.ComponentModel;

using Microsoft.Extensions.Logging;

using Avalonia.Media.Imaging;

using Pica.Protocol;

namespace Pica.Viewer.Services;

internal sealed class ImageLoadCoordinator :
    IImageLoadingSettings,
    IDisposable
{
    internal bool IsFullResolutionReady => _isFullResolutionReady;

    private readonly ImageViewerSession _session;
    private readonly ImagePreviewLoader _imagePreviewLoader;
    private readonly FullResolutionImageLoader _fullResolutionImageLoader;
    private readonly IImageLoadPresentationSink _presentationSink;
    private readonly IViewerRenderFrameAwaiter _renderFrameAwaiter;
    private readonly IViewerUiDispatcher _uiDispatcher;
    private readonly ILogger<ImageLoadCoordinator> _logger;
    private readonly ImagePreviewPrefetcher _previewPrefetcher;
    private bool _isFastLoadingEnabled;
    private bool _isFullResolutionReady;
    private bool _isStarted;
    private bool _disposed;
    private long _loadId;
    private OperationCancellation? _loadCancellation;
    private Task? _activeLoadTask;
    private Task? _previewCachePrimingTask;

    internal ImageLoadCoordinator(
        ImageViewerSession session,
        ImagePreviewLoader imagePreviewLoader,
        FullResolutionImageLoader fullResolutionImageLoader,
        IImageLoadPresentationSink presentationSink,
        IViewerRenderFrameAwaiter renderFrameAwaiter,
        IViewerUiDispatcher uiDispatcher,
        ILogger<ImageLoadCoordinator> logger,
        ILogger<ImagePreviewPrefetcher> previewPrefetcherLogger,
        bool isFastLoadingEnabled)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _imagePreviewLoader = imagePreviewLoader
            ?? throw new ArgumentNullException(nameof(imagePreviewLoader));
        _fullResolutionImageLoader = fullResolutionImageLoader
            ?? throw new ArgumentNullException(nameof(fullResolutionImageLoader));
        _presentationSink = presentationSink
            ?? throw new ArgumentNullException(nameof(presentationSink));
        _renderFrameAwaiter = renderFrameAwaiter
            ?? throw new ArgumentNullException(nameof(renderFrameAwaiter));
        _uiDispatcher = uiDispatcher
            ?? throw new ArgumentNullException(nameof(uiDispatcher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _previewPrefetcher = new ImagePreviewPrefetcher(
            session,
            imagePreviewLoader,
            previewPrefetcherLogger);
        _isFastLoadingEnabled = isFastLoadingEnabled;
    }

    public void SetFastLoadingEnabled(bool isFastLoadingEnabled)
    {
        ThrowIfDisposed();
        _isFastLoadingEnabled = isFastLoadingEnabled;

        if (_isFastLoadingEnabled)
        {
            StartPreviewCachePriming();
            return;
        }

        if (_previewCachePrimingTask is not null)
        {
            CancelPendingWork();
        }

        _previewPrefetcher.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_isStarted)
        {
            _session.PropertyChanged -= OnSessionPropertyChanged;
        }

        CancelPendingWork();
        _previewPrefetcher.Dispose();
        _disposed = true;
    }

    internal void Start()
    {
        ThrowIfDisposed();

        if (_isStarted)
        {
            return;
        }

        _isStarted = true;
        _session.PropertyChanged += OnSessionPropertyChanged;
        LoadSelectedImage();
    }

    internal async Task<bool> WaitForFullResolutionAsync(CancellationToken ct)
    {
        ThrowIfDisposed();

        if (_isFullResolutionReady)
        {
            return true;
        }

        Task? loadTask = _activeLoadTask;

        if (loadTask is not null)
        {
            await loadTask
                .WaitAsync(ct)
                .ConfigureAwait(false);

            return object.ReferenceEquals(loadTask, _activeLoadTask)
                && _isFullResolutionReady;
        }

        return _isFullResolutionReady;
    }

    private static string GetExistingImagePath(PicaImageItem item)
    {
        string fullPath = Path.GetFullPath(item.FilePath);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                "The image selected for Pica does not exist.",
                fullPath);
        }

        return fullPath;
    }

    private void LoadSelectedImage()
    {
        PicaImageItem? item = _session.SelectedItem;

        if (item is null)
        {
            return;
        }

        StartImageLoad(item);
        bool useFastLoading = _isFastLoadingEnabled;
        int selectedIndex = _session.SelectedIndex;
        long loadId = _loadId;
        OperationCancellation cancellation = _loadCancellation
            ?? throw new InvalidOperationException(
                "An image load cancellation source was not created.");
        _activeLoadTask = LoadSelectedImageAsync(
            item,
            selectedIndex,
            loadId,
            useFastLoading,
            cancellation);
    }

    private async Task LoadSelectedImageAsync(
        PicaImageItem item,
        int selectedIndex,
        long loadId,
        bool useFastLoading,
        OperationCancellation cancellation)
    {
        CancellationToken ct = cancellation.Token;

        try
        {
            string fullPath;

            try
            {
                fullPath = await Task.Run(
                    () => GetExistingImagePath(item),
                    ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException ex) when (ct.IsCancellationRequested)
            {
                _logger.LogDebug(
                    ex,
                    "Cancelled path validation for Pica image {ItemId}.",
                    item.Id);

                return;
            }
            catch (Exception ex)
            {
                LogFullResolutionImageLoadFailure(ex, item);
                return;
            }

            if (useFastLoading)
            {
                await LoadProgressivelyAsync(
                    item,
                    fullPath,
                    selectedIndex,
                    loadId,
                    ct).ConfigureAwait(false);
                return;
            }

            await LoadFullResolutionAsync(
                item,
                fullPath,
                loadId,
                ct).ConfigureAwait(false);
        }
        finally
        {
            if (object.ReferenceEquals(_loadCancellation, cancellation))
            {
                _loadCancellation = null;
            }

            cancellation.Complete();
        }
    }

    private void StartImageLoad(PicaImageItem item)
    {
        CancelPendingWork();
        OperationCancellation cancellation = new();
        _loadCancellation = cancellation;
        _loadId++;
        _isFullResolutionReady = false;
        _presentationSink.BeginImageLoad(item);
    }

    private async Task LoadFullResolutionAsync(
        PicaImageItem item,
        string fullPath,
        long loadId,
        CancellationToken ct)
    {
        Bitmap? bitmap = null;

        try
        {
            bitmap = await _fullResolutionImageLoader
                .LoadAsync(fullPath, ct)
                .ConfigureAwait(false);

            Bitmap loadedBitmap = bitmap;
            bool isOwnershipTransferred = false;
            await _uiDispatcher.InvokeAsync(
                () =>
                {
                    if (!CanApplyLoad(loadId, ct))
                    {
                        return;
                    }

                    _presentationSink.ApplyFullResolution(
                        item,
                        fullPath,
                        null,
                        loadedBitmap);
                    _isFullResolutionReady = true;
                    isOwnershipTransferred = true;
                },
                ct).ConfigureAwait(false);

            if (!isOwnershipTransferred)
            {
                return;
            }

            bitmap = null;
            _logger.LogInformation(
                "Loaded Pica image {ItemId} at full resolution {Width}x{Height}",
                item.Id,
                loadedBitmap.PixelSize.Width,
                loadedBitmap.PixelSize.Height);
        }
        catch (OperationCanceledException ex) when (ct.IsCancellationRequested)
        {
            _logger.LogDebug(
                ex,
                "Cancelled full-resolution load for Pica image {ItemId}",
                item.Id);
        }
        catch (Exception ex)
        {
            LogFullResolutionImageLoadFailure(ex, item);
        }
        finally
        {
            bitmap?.Dispose();
        }
    }

    private async Task LoadProgressivelyAsync(
        PicaImageItem item,
        string fullPath,
        int selectedIndex,
        long loadId,
        CancellationToken ct)
    {
        DecodedImagePreview? preview =
            _previewPrefetcher.Take(fullPath);
        Bitmap? fullResolutionBitmap = null;
        bool isPreviewOwnershipTransferred = false;

        try
        {
            preview ??= await TryDecodePreviewAsync(item, ct)
                .ConfigureAwait(false);

            if (preview is not null)
            {
                DecodedImagePreview loadedPreview = preview;
                bool isPreviewApplied = false;
                await _uiDispatcher.InvokeAsync(
                    () =>
                    {
                        if (!CanApplyLoad(loadId, ct))
                        {
                            return;
                        }

                        _presentationSink.ApplyPreview(
                            item,
                            fullPath,
                            loadedPreview);
                        isPreviewApplied = true;
                    },
                    ct).ConfigureAwait(false);

                if (!isPreviewApplied)
                {
                    return;
                }

                isPreviewOwnershipTransferred = true;
                await WaitForNextRenderFrameAsync(ct)
                    .ConfigureAwait(false);
            }

            fullResolutionBitmap = await _fullResolutionImageLoader
                .LoadAsync(fullPath, ct)
                .ConfigureAwait(false);

            Bitmap loadedBitmap = fullResolutionBitmap;
            DecodedImagePreview? displayedPreview =
                isPreviewOwnershipTransferred
                    ? preview
                    : null;
            bool isFullResolutionApplied = false;
            await _uiDispatcher.InvokeAsync(
                () =>
                {
                    if (!CanApplyLoad(loadId, ct))
                    {
                        return;
                    }

                    _presentationSink.ApplyFullResolution(
                        item,
                        fullPath,
                        displayedPreview,
                        loadedBitmap);
                    _isFullResolutionReady = true;
                    isFullResolutionApplied = true;
                },
                ct).ConfigureAwait(false);

            if (!isFullResolutionApplied)
            {
                return;
            }

            fullResolutionBitmap = null;
            _logger.LogInformation(
                "Progressively loaded Pica image {ItemId} at full resolution {Width}x{Height}",
                item.Id,
                loadedBitmap.PixelSize.Width,
                loadedBitmap.PixelSize.Height);

            await PrefetchAdjacentPreviewBitmapsAsync(
                selectedIndex,
                loadId,
                ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (ct.IsCancellationRequested)
        {
            _logger.LogDebug(
                ex,
                "Cancelled progressive load for Pica image {ItemId}",
                item.Id);
        }
        catch (Exception ex)
        {
            LogFullResolutionImageLoadFailure(ex, item);
        }
        finally
        {
            if (!isPreviewOwnershipTransferred)
            {
                preview?.Bitmap.Dispose();
            }

            fullResolutionBitmap?.Dispose();
        }
    }

    private async Task PrefetchAdjacentPreviewBitmapsAsync(
        int selectedIndex,
        long loadId,
        CancellationToken ct)
    {
        if (!_isFastLoadingEnabled)
        {
            _previewPrefetcher.Clear();
            return;
        }

        await _previewPrefetcher.PrefetchAdjacentAsync(
            selectedIndex,
            () => _isFastLoadingEnabled && CanApplyLoad(loadId, ct),
            ct).ConfigureAwait(false);
    }

    private async Task WaitForNextRenderFrameAsync(CancellationToken ct)
    {
        Task frameWaitTask = await _uiDispatcher.InvokeAsync(
            () => _renderFrameAwaiter.WaitAsync(ct),
            ct).ConfigureAwait(false);
        await frameWaitTask
            .WaitAsync(ct)
            .ConfigureAwait(false);
    }

    private void StartPreviewCachePriming()
    {
        if (!_isFullResolutionReady)
        {
            return;
        }

        CancelPendingWork();
        OperationCancellation cancellation = new();
        long loadId = ++_loadId;
        _loadCancellation = cancellation;
        Task primingTask = PrimePreviewCacheAsync(
            _session.SelectedIndex,
            loadId,
            cancellation);
        _previewCachePrimingTask = primingTask.IsCompleted
            ? null
            : primingTask;
    }

    private async Task PrimePreviewCacheAsync(
        int selectedIndex,
        long loadId,
        OperationCancellation cancellation)
    {
        try
        {
            await PrefetchAdjacentPreviewBitmapsAsync(
                selectedIndex,
                loadId,
                cancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
            when (cancellation.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to preload neighboring images.");
        }
        finally
        {
            if (object.ReferenceEquals(_loadCancellation, cancellation))
            {
                _loadCancellation = null;
                _previewCachePrimingTask = null;
            }

            cancellation.Complete();
        }
    }

    private async Task<DecodedImagePreview?> TryDecodePreviewAsync(
        PicaImageItem item,
        CancellationToken ct)
    {
        try
        {
            return await _imagePreviewLoader
                .LoadAsync(item, ct)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to load the preview for image {ItemId}.",
                item.Id);

            return null;
        }
    }

    private bool CanApplyLoad(long loadId, CancellationToken ct)
    {
        return !ct.IsCancellationRequested && (loadId == _loadId);
    }

    private void CancelPendingWork()
    {
        _loadId++;
        OperationCancellation? cancellation = _loadCancellation;
        _loadCancellation = null;
        _activeLoadTask = null;
        _previewCachePrimingTask = null;
        cancellation?.Cancel();
    }

    private void LogFullResolutionImageLoadFailure(
        Exception exception,
        PicaImageItem item)
    {
        _logger.LogError(
            exception,
            "Failed to load image {ItemId} at full resolution.",
            item.Id);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private void OnSessionPropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        _ = sender;

        if (string.Equals(
            e.PropertyName,
            nameof(ImageViewerSession.SelectedIndex),
            StringComparison.Ordinal))
        {
            LoadSelectedImage();
        }
    }
}
