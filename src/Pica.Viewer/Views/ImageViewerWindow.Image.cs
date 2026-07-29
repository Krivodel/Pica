using Microsoft.Extensions.Logging;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using SukiUI.Controls;

using Pica.Protocol;
using Pica.Viewer.Services;

namespace Pica.Viewer.Views;

public sealed partial class ImageViewerWindow : SukiWindow
{
    private void LoadSelectedImage()
    {
        if (!TryGetSelectedItem(out PicaImageItem? item) || item is null)
        {
            return;
        }

        UpdateImageInformation(item, new PixelSize());
        ResetPanMotion();

        if (_settings.IsFastLoadingEnabled)
        {
            StartProgressiveImageLoad(item);
            return;
        }

        StartFullResolutionImageLoad(item);
    }

    private void StartFullResolutionImageLoad(PicaImageItem item)
    {
        string fullPath = GetExistingImagePath(item);

        StartImageLoad((loadId, ct) => LoadFullResolutionImageAsync(
            item,
            fullPath,
            loadId,
            ct));
    }

    private async Task LoadFullResolutionImageAsync(
        PicaImageItem item,
        string fullPath,
        long loadId,
        CancellationToken ct)
    {
        try
        {
            Bitmap bitmap = await _fullResolutionImageLoader.LoadAsync(fullPath, ct);

            if (!CanApplyImageLoad(loadId, ct))
            {
                bitmap.Dispose();
                return;
            }

            ReplaceDisplayedBitmap(item, fullPath, bitmap, bitmap.PixelSize);
            _isFullResolutionImageReady = true;
            StartSelectedChannelLoad();
            _logger.LogInformation(
                "Loaded Pica image {ItemId} at full resolution {Width}x{Height}",
                item.Id,
                bitmap.PixelSize.Width,
                bitmap.PixelSize.Height);

            ApplyLoadedImageLayout(out bool fittedWindow);

            if (fittedWindow)
            {
                return;
            }
        }
        catch (OperationCanceledException ex) when (ct.IsCancellationRequested)
        {
            _logger.LogDebug(
                ex,
                "Cancelled full-resolution load for Pica image {ItemId}",
                item.Id);
            return;
        }
        catch (Exception ex)
        {
            LogFullResolutionImageLoadFailure(ex, item);
        }
    }

    private void StartProgressiveImageLoad(PicaImageItem item)
    {
        string fullPath = GetExistingImagePath(item);

        StartImageLoad((loadId, ct) => LoadFileImageProgressivelyAsync(
            item,
            fullPath,
            _selectedIndex,
            loadId,
            ct));
    }

    private void StartImageLoad(Func<long, CancellationToken, Task> load)
    {
        ArgumentNullException.ThrowIfNull(load);
        CancelPendingImageLoad();
        CancellationTokenSource cancellation = new();
        long loadId = ++_imageLoadId;
        _imageLoadCancellation = cancellation;
        _isFullResolutionImageReady = false;
        CancelSelection();
        _activeImageLoadTask = load(loadId, cancellation.Token);
    }

    private async Task LoadFileImageProgressivelyAsync(
        PicaImageItem item,
        string fullPath,
        int selectedIndex,
        long loadId,
        CancellationToken ct)
    {
        DecodedImagePreview? preview = _previewCache.Take(fullPath);

        try
        {
            preview ??= await TryDecodePreviewAsync(item, ct);

            if (preview is not null)
            {
                if (!CanApplyImageLoad(loadId, ct))
                {
                    preview.Bitmap.Dispose();
                    return;
                }

                CancelSelection();
                ReplaceDisplayedBitmap(
                    item,
                    fullPath,
                    preview.Bitmap,
                    preview.SourcePixelSize);
                ApplyLoadedImageLayout(out _);
                await WaitForNextRenderFrameAsync(ct);
            }

            Bitmap fullResolutionBitmap = await _fullResolutionImageLoader
                .LoadAsync(fullPath, ct);

            if (!CanApplyImageLoad(loadId, ct))
            {
                fullResolutionBitmap.Dispose();
                return;
            }

            ReplacePreviewWithFullResolutionBitmap(
                item,
                fullPath,
                preview,
                fullResolutionBitmap);
            _isFullResolutionImageReady = true;
            StartSelectedChannelLoad();
            _logger.LogInformation(
                "Progressively loaded Pica image {ItemId} at full resolution {Width}x{Height}",
                item.Id,
                fullResolutionBitmap.PixelSize.Width,
                fullResolutionBitmap.PixelSize.Height);

            await PrefetchAdjacentPreviewBitmapsCoreAsync(selectedIndex, loadId, ct);
        }
        catch (OperationCanceledException ex) when (ct.IsCancellationRequested)
        {
            _logger.LogDebug(
                ex,
                "Cancelled progressive load for Pica image {ItemId}",
                item.Id);
            return;
        }
        catch (Exception ex)
        {
            LogFullResolutionImageLoadFailure(ex, item);
        }
    }

    private string GetExistingImagePath(PicaImageItem item)
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

    private void LogFullResolutionImageLoadFailure(
        Exception exception,
        PicaImageItem item)
    {
        _logger.LogError(
            exception,
            "Failed to load image {ItemId} at full resolution.",
            item.Id);
    }

    private async Task WaitForNextRenderFrameAsync(CancellationToken ct)
    {
        TaskCompletionSource frameRendered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        RequestViewerAnimationFrame(_ => frameRendered.TrySetResult());

        await frameRendered.Task.WaitAsync(ct);
    }

    private async Task PrefetchAdjacentPreviewBitmapsAsync(
        int selectedIndex,
        long loadId,
        CancellationToken ct)
    {
        if (!_settings.IsFastLoadingEnabled)
        {
            _previewCache.Clear();
            return;
        }

        await PrefetchAdjacentPreviewBitmapsCoreAsync(selectedIndex, loadId, ct);
    }

    private async Task PrefetchAdjacentPreviewBitmapsCoreAsync(
        int selectedIndex,
        long loadId,
        CancellationToken ct)
    {
        List<PicaImageItem> adjacentItems = PrepareAdjacentPreviewCache(selectedIndex);

        foreach (PicaImageItem adjacentItem in adjacentItems)
        {
            await PrefetchPreviewBitmapAsync(adjacentItem, loadId, ct);
        }
    }

    private List<PicaImageItem> PrepareAdjacentPreviewCache(int selectedIndex)
    {
        List<PicaImageItem> adjacentItems = GetAdjacentImageItems(selectedIndex);
        List<string> adjacentPaths = adjacentItems
            .Select(item => Path.GetFullPath(item.FilePath))
            .ToList();
        _previewCache.Retain(adjacentPaths);

        return adjacentItems;
    }

    private async Task PrefetchPreviewBitmapAsync(
        PicaImageItem item,
        long loadId,
        CancellationToken ct)
    {
        string fullPath = Path.GetFullPath(item.FilePath);

        if (_previewCache.Contains(fullPath))
        {
            return;
        }

        DecodedImagePreview? preview = await TryDecodePreviewAsync(item, ct);

        if (preview is null)
        {
            return;
        }

        if (!_settings.IsFastLoadingEnabled || !CanApplyImageLoad(loadId, ct))
        {
            preview.Bitmap.Dispose();
            _previewCache.Clear();
            return;
        }

        _previewCache.Store(fullPath, preview);
    }

    private List<PicaImageItem> GetAdjacentImageItems(int selectedIndex)
    {
        IReadOnlyList<PicaImageItem> items = _request.Items;
        List<PicaImageItem> adjacentItems = [];

        if (items.Count <= 1)
        {
            return adjacentItems;
        }

        int normalizedIndex = Math.Clamp(selectedIndex, 0, items.Count - 1);
        int[] offsets = [_preferredNavigationDirection, -_preferredNavigationDirection];

        foreach (int offset in offsets)
        {
            int adjacentIndex = (normalizedIndex + offset + items.Count) % items.Count;
            PicaImageItem adjacentItem = items[adjacentIndex];
            string adjacentPath = Path.GetFullPath(adjacentItem.FilePath);

            if (File.Exists(adjacentPath)
                && !adjacentItems.Any(item => string.Equals(
                    Path.GetFullPath(item.FilePath),
                    adjacentPath,
                    StringComparison.OrdinalIgnoreCase)))
            {
                adjacentItems.Add(adjacentItem);
            }
        }

        return adjacentItems;
    }

    private void StartPreviewCachePriming()
    {
        if (!_isFullResolutionImageReady)
        {
            return;
        }

        CancelPendingImageLoad();
        CancellationTokenSource cancellation = new();
        long loadId = ++_imageLoadId;
        _imageLoadCancellation = cancellation;
        Task primingTask = PrimePreviewCacheAsync(
            _selectedIndex,
            loadId,
            cancellation);
        _previewCachePrimingTask = primingTask.IsCompleted
            ? null
            : primingTask;
    }

    private async Task PrimePreviewCacheAsync(
        int selectedIndex,
        long loadId,
        CancellationTokenSource cancellation)
    {
        try
        {
            await PrefetchAdjacentPreviewBitmapsAsync(
                selectedIndex,
                loadId,
                cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to preload neighboring images.");
        }
        finally
        {
            if (ReferenceEquals(_imageLoadCancellation, cancellation))
            {
                _imageLoadCancellation = null;
                _previewCachePrimingTask = null;
                cancellation.Dispose();
            }
        }
    }

    private async Task<DecodedImagePreview?> TryDecodePreviewAsync(
        PicaImageItem item,
        CancellationToken ct)
    {
        try
        {
            return await _imagePreviewLoader.LoadAsync(item, ct);
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

    private void ReplacePreviewWithFullResolutionBitmap(
        PicaImageItem item,
        string fullPath,
        DecodedImagePreview? preview,
        Bitmap fullResolutionBitmap)
    {
        Bitmap? previewBitmap = preview?.Bitmap;
        bool isPreviewDisplayed = previewBitmap is not null && ReferenceEquals(_bitmap, previewBitmap);
        PixelSize previewSize = previewBitmap?.PixelSize ?? new PixelSize();
        PixelRect previewSelection = _selectionPixelRect;
        double previewScale = _scale;
        ReplaceDisplayedBitmap(
            item,
            fullPath,
            fullResolutionBitmap,
            fullResolutionBitmap.PixelSize);

        if (!isPreviewDisplayed || (previewSize.Width <= 0) || (previewSize.Height <= 0))
        {
            CancelSelection();
            ApplyLoadedImageLayout(out _);
            return;
        }

        _scale = previewScale * previewSize.Width / fullResolutionBitmap.PixelSize.Width;

        if (_isSelectionActive)
        {
            SetSelectionPixelRect(ScalePixelRect(
                previewSelection,
                previewSize,
                fullResolutionBitmap.PixelSize));
        }

        ApplyImageLayout();
        ResetPanMotion();
    }

    private void ReplaceDisplayedBitmap(
        PicaImageItem item,
        string fullPath,
        Bitmap bitmap,
        PixelSize sourcePixelSize)
    {
        PicaImageItem displayedItem = item with { FilePath = fullPath };
        ReplaceSourceBitmap(bitmap);
        _currentItem = displayedItem;
        _sourcePixelSize = sourcePixelSize;
        UpdateImageInformation(displayedItem, sourcePixelSize);
    }

    private void UpdateSelectedImageInformation()
    {
        if (TryGetSelectedItem(out PicaImageItem? item) && (item is not null))
        {
            PicaImageItem informationItem = item;
            PixelSize sourcePixelSize = new();

            if (_currentItem is { } currentItem
                && currentItem.Id == item.Id)
            {
                informationItem = currentItem;
                sourcePixelSize = _sourcePixelSize;
            }

            UpdateImageInformation(informationItem, sourcePixelSize);
        }
    }

    private void UpdateImageInformation(
        PicaImageItem item,
        PixelSize sourcePixelSize)
    {
        ImageViewerInformationOptions options = new(
            _settings.ShowImageName,
            _settings.ShowImageFormat,
            _settings.ShowImageResolution,
            _settings.ShowImageModificationDate);
        string information = ImageViewerInformationFormatter.Format(
            item.FileName,
            sourcePixelSize,
            _channelSelection.SelectedChannel,
            _settings.ShowImageModificationDate
                ? GetModificationDate(item.FilePath)
                : null,
            options);
        Title = information;
        _view.UpdateImageInformation(information);
        UpdateImageInformationPanelVisibility();
    }

    private static DateTime? GetModificationDate(string filePath)
    {
        try
        {
            return File.Exists(filePath)
                ? File.GetLastWriteTime(filePath)
                : null;
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or NotSupportedException)
        {
            return null;
        }
    }

    private void ReleaseDisplayedBitmap()
    {
        ReleaseImageBitmaps();
    }

    private void ApplyLoadedImageLayout(out bool fittedWindow)
    {
        if (_isWindowedMode
            && (_settings.ResizeBehavior == WindowResizeBehavior.AlwaysFitImage))
        {
            FitWindowToCurrentImage();
            fittedWindow = true;
            return;
        }

        ResetScaleAndCenter();
        fittedWindow = false;
    }

    private static PixelRect ScalePixelRect(
        PixelRect pixelRect,
        PixelSize sourceSize,
        PixelSize targetSize)
    {
        double scaleX = (double)targetSize.Width / sourceSize.Width;
        double scaleY = (double)targetSize.Height / sourceSize.Height;
        int left = Math.Clamp((int)Math.Floor(pixelRect.X * scaleX), 0, targetSize.Width - 1);
        int top = Math.Clamp((int)Math.Floor(pixelRect.Y * scaleY), 0, targetSize.Height - 1);
        int right = Math.Clamp(
            (int)Math.Ceiling((pixelRect.X + pixelRect.Width) * scaleX),
            left + 1,
            targetSize.Width);
        int bottom = Math.Clamp(
            (int)Math.Ceiling((pixelRect.Y + pixelRect.Height) * scaleY),
            top + 1,
            targetSize.Height);

        return new PixelRect(left, top, right - left, bottom - top);
    }

    private bool CanApplyImageLoad(long loadId, CancellationToken ct)
    {
        return !ct.IsCancellationRequested && (loadId == _imageLoadId);
    }

    private void CancelPendingImageLoad()
    {
        _imageLoadId++;
        CancellationTokenSource? cancellation = _imageLoadCancellation;
        _imageLoadCancellation = null;
        _activeImageLoadTask = null;
        _previewCachePrimingTask = null;
        cancellation?.Cancel();
        cancellation?.Dispose();
    }

    private async Task<bool> WaitForFullResolutionImageAsync(CancellationToken ct)
    {
        if (_isFullResolutionImageReady)
        {
            return true;
        }

        Task? loadTask = _activeImageLoadTask;

        if (loadTask is not null)
        {
            await loadTask.WaitAsync(ct);

            return ReferenceEquals(loadTask, _activeImageLoadTask)
                && _isFullResolutionImageReady;
        }

        return _isFullResolutionImageReady;
    }

    private void ResetScaleAndCenter()
    {
        if (!TryGetResetImagePlacement(out double targetScale, out double targetOffsetX, out double targetOffsetY))
        {
            return;
        }

        _scale = targetScale;
        _offsetX = targetOffsetX;
        _offsetY = targetOffsetY;
        ApplyImageLayout();
        ResetPanMotion();
    }

    private void BeginResetScaleAndCenterAnimation()
    {
        ResetPanMotion();

        if (!TryGetResetImagePlacement(out double targetScale, out double targetOffsetX, out double targetOffsetY))
        {
            return;
        }

        double startScale = _scale;
        double startOffsetX = _offsetX;
        double startOffsetY = _offsetY;

        StartScaleFrameAnimation(
            easedProgress =>
            {
                _scale = startScale + ((targetScale - startScale) * easedProgress);
                _offsetX = startOffsetX + ((targetOffsetX - startOffsetX) * easedProgress);
                _offsetY = startOffsetY + ((targetOffsetY - startOffsetY) * easedProgress);
                ApplyImageLayout();
            });
    }

    private bool TryGetResetImagePlacement(
        out double targetScale,
        out double targetOffsetX,
        out double targetOffsetY)
    {
        targetScale = _scale;
        targetOffsetX = _offsetX;
        targetOffsetY = _offsetY;

        if (_bitmap is null)
        {
            return false;
        }

        Size viewport = GetViewportSize();
        if ((viewport.Width <= 0d) || (viewport.Height <= 0d))
        {
            return false;
        }

        double viewportPixelWidth = viewport.Width * RenderScaling;
        double viewportPixelHeight = viewport.Height * RenderScaling;
        PixelSize sourcePixelSize = GetCurrentSourcePixelSize();
        double fittedScale = ImageWindowGeometry.CalculateFittedScale(
            sourcePixelSize,
            new Size(viewportPixelWidth, viewportPixelHeight));
        double sourceScale = fittedScale;
        targetScale = sourcePixelSize.Width * sourceScale / _bitmap.PixelSize.Width;
        double targetImageWidth = _bitmap.PixelSize.Width * targetScale / RenderScaling;
        double targetImageHeight = _bitmap.PixelSize.Height * targetScale / RenderScaling;
        targetOffsetX = (viewport.Width - targetImageWidth) / 2d;
        targetOffsetY = (viewport.Height - targetImageHeight) / 2d;

        return true;
    }

    private PixelSize GetCurrentSourcePixelSize()
    {
        if (_sourcePixelSize is { Width: > 0, Height: > 0 })
        {
            return _sourcePixelSize;
        }

        return _bitmap?.PixelSize ?? new PixelSize();
    }

    private void ApplyImageLayout()
    {
        if (_bitmap is null)
        {
            return;
        }

        _view.Image.Width = GetImageDipWidth();
        _view.Image.Height = GetImageDipHeight();
        ClampImageOffset();
        Canvas.SetLeft(_view.Image, _offsetX);
        Canvas.SetTop(_view.Image, _offsetY);

        if (_isSelectionActive && !_isSelecting)
        {
            SetSelectionPixelRect(_selectionPixelRect);
            UpdateSelectionOverlay();
        }
    }

    private void ClampImageOffset()
    {
        if (!TryGetCurrentPanBounds(out Rect bounds))
        {
            return;
        }

        Point offset = ImageWindowGeometry.ClampOffset(
            new Point(_offsetX, _offsetY),
            bounds);
        _offsetX = offset.X;
        _offsetY = offset.Y;
    }

    private bool TryGetCurrentPanBounds(out Rect bounds)
    {
        Size imageSize = new(GetImageDipWidth(), GetImageDipHeight());
        Size viewportSize = GetViewportSize();
        bounds = new Rect();

        if ((imageSize.Width <= 0d)
            || (imageSize.Height <= 0d)
            || (viewportSize.Width <= 0d)
            || (viewportSize.Height <= 0d))
        {
            return false;
        }

        bounds = ImageWindowGeometry.GetPanBounds(imageSize, viewportSize);

        return true;
    }

    private double GetImageDipWidth()
    {
        return _bitmap is null ? 0d : _bitmap.PixelSize.Width * _scale / RenderScaling;
    }

    private double GetImageDipHeight()
    {
        return _bitmap is null ? 0d : _bitmap.PixelSize.Height * _scale / RenderScaling;
    }

    private Size GetViewportSize()
    {
        return _view.ViewerArea.Bounds.Size;
    }

    private void Navigate(int direction)
    {
        if (_channelSelection.IsActive)
        {
            NavigateChannel(direction);
            return;
        }

        IReadOnlyList<PicaImageItem> items = _request.Items;
        if (items.Count == 0)
        {
            return;
        }

        int currentIndex = Math.Clamp(_selectedIndex, 0, items.Count - 1);
        _preferredNavigationDirection = direction < 0 ? -1 : 1;
        _selectedIndex = (currentIndex + direction + items.Count) % items.Count;
        LoadSelectedImage();
    }

    private bool TryGetSelectedItem(out PicaImageItem? item)
    {
        IReadOnlyList<PicaImageItem> items = _request.Items;
        if (items.Count == 0)
        {
            item = null;

            return false;
        }

        _selectedIndex = Math.Clamp(_selectedIndex, 0, items.Count - 1);
        item = items[_selectedIndex];

        return true;
    }

    private static int GetItemIndexOrDefault(
        IReadOnlyList<PicaImageItem> items,
        Guid itemId)
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].Id == itemId)
            {
                return i;
            }
        }

        return items.Count == 0 ? -1 : 0;
    }

    private void BeginScaleAnimation(double targetScale, Point anchor)
    {
        if (_bitmap is null)
        {
            return;
        }

        ResetPanMotion();
        double startScale = _scale;
        double minimumScale = MinimumScale;
        double maximumScale = MaximumScale;

        if (TryGetResetImagePlacement(out double fittedScale, out _, out _))
        {
            maximumScale = Math.Max(maximumScale, fittedScale);

            if (!_settings.AllowFreeZoomOut)
            {
                minimumScale = fittedScale;
            }
        }

        double clampedScale = Math.Clamp(targetScale, minimumScale, maximumScale);
        double imageX = (anchor.X - _offsetX) / GetImageDipWidth();
        double imageY = (anchor.Y - _offsetY) / GetImageDipHeight();

        if (_isPanning)
        {
            StopScaleAnimation();
            ApplyScaleAtAnchor(clampedScale, anchor, imageX, imageY);
            ResetPanMotion();
            return;
        }

        StartScaleFrameAnimation(
            easedProgress =>
            {
                double frameScale = startScale + ((clampedScale - startScale) * easedProgress);
                ApplyScaleAtAnchor(frameScale, anchor, imageX, imageY);
            });
    }

    private void StartScaleFrameAnimation(Action<double> applyFrame)
    {
        long animationId = ++_scaleAnimationId;

        StartFrameAnimation(
            ScaleAnimationDuration,
            () => animationId == _scaleAnimationId,
            progress => applyFrame(EaseOutCubic(progress)));
    }

    private void ApplyScaleAtAnchor(
        double scale,
        Point anchor,
        double imageX,
        double imageY)
    {
        _scale = scale;
        _offsetX = anchor.X - (imageX * GetImageDipWidth());
        _offsetY = anchor.Y - (imageY * GetImageDipHeight());
        ApplyImageLayout();
    }

    private void StopScaleAnimation()
    {
        _scaleAnimationId++;
    }
}
