using Microsoft.Extensions.Logging;

using Avalonia.Media.Imaging;
using SukiUI.Controls;

using Pica.Protocol;
using Pica.Viewer.Services;

namespace Pica.Viewer.Views;

public sealed partial class ImageViewerWindow : SukiWindow
{
    private async Task CopyCurrentImageAsync(CancellationToken ct)
    {
        await RunExclusiveImageOperationAsync(CopyCurrentImageCoreAsync, ct);
    }

    private async Task CopyCurrentImageCoreAsync(CancellationToken ct)
    {
        if (_channelSelection.IsActive)
        {
            PreparedClipboardImage? channelImage =
                await PrepareCurrentChannelImageAsync(ct);

            if (channelImage is null)
            {
                return;
            }

            await _imageOperations.CopyPreparedImageAsync(channelImage, ct);
            _logger.LogInformation(
                "Copied channel {Channel} of Pica image {ItemId} to the clipboard",
                _channelSelection.SelectedChannel?.Code,
                _currentItem?.Id);
            return;
        }

        bool isFullResolutionReady = await WaitForFullResolutionImageAsync(ct);
        PicaImageItem? item = _currentItem;

        if (item is null)
        {
            return;
        }

        Bitmap? bitmap = isFullResolutionReady ? _bitmap : null;
        await _imageOperations.CopyFileAsync(StorageProvider, item, bitmap, ct);
        _logger.LogInformation("Copied Pica image {ItemId} to the clipboard", item.Id);
    }

    private async Task CopyCurrentImageWithFeedbackAsync(CancellationToken ct)
    {
        await CopyCurrentImageAsync(ct);
        await ShowCopyFeedbackAsync();
    }

    private async Task ShowCopyFeedbackAsync()
    {
        long animationId = ++_copyFeedbackAnimationId;

        await AnimateCopyFeedbackOpacityAsync(
            animationId,
            ImageViewerVisualMetrics.HiddenControlsOpacity,
            CopyFeedbackOpacity,
            CopyFeedbackFadeInDuration);
        await AnimateCopyFeedbackOpacityAsync(
            animationId,
            CopyFeedbackOpacity,
            ImageViewerVisualMetrics.HiddenControlsOpacity,
            CopyFeedbackFadeOutDuration);
    }

    private Task AnimateCopyFeedbackOpacityAsync(
        long animationId,
        double from,
        double to,
        TimeSpan duration)
    {
        TaskCompletionSource completion = new();
        StartFrameAnimation(
            duration,
            () => animationId == _copyFeedbackAnimationId,
            progress =>
            {
                _view.FadeOverlay.Opacity = from + ((to - from) * progress);
            },
            () => completion.TrySetResult(),
            () => completion.TrySetResult());

        return completion.Task;
    }

    private async Task DispatchCurrentImageActionAsync(
        PicaActionDefinition action,
        CancellationToken ct)
    {
        if (_currentItem is null)
        {
            return;
        }

        await _imageOperations.DispatchCurrentAsync(action, _currentItem, ct);
        _logger.LogInformation(
            "Dispatched Pica action {ActionId} for image {ItemId}",
            action.Id,
            _currentItem.Id);
    }

    private async Task CopySelectionAsync(CancellationToken ct)
    {
        bool isFullResolutionReady =
            await WaitForCurrentImagePresentationAsync(ct);

        if (!isFullResolutionReady)
        {
            return;
        }

        PreparedClipboardImage? image = await GetPreparedSelectionClipboardImageAsync(ct);

        if (image is null)
        {
            return;
        }

        await _imageOperations.CopyPreparedImageAsync(image, ct);
        _logger.LogInformation(
            "Copied Pica image selection with {ByteCount} encoded bytes to the clipboard",
            image.PngContent.Length);
    }

    private async Task CopySelectionAndCloseAsync(CancellationToken ct)
    {
        await RunExclusiveImageOperationAsync(
            async operationCt =>
            {
                await CopySelectionAsync(operationCt);
                CancelSelection();
            },
            ct);
    }

    private async Task DispatchSelectionActionAndCloseAsync(
        PicaActionDefinition action,
        CancellationToken ct)
    {
        await RunWithFullResolutionSelectionAsync(
            async (bitmap, operationCt) =>
            {
                PicaImageItem? item = _currentItem;

                if (item is null)
                {
                    return;
                }

                CancelSelection();

                await _imageOperations.DispatchSelectionAsync(
                    action,
                    item,
                    bitmap,
                    operationCt);
                _logger.LogInformation(
                    "Dispatched Pica selection action {ActionId} for image {ItemId}",
                    action.Id,
                    item.Id);
            },
            ct);
    }

    private async Task SaveCurrentImageAsAsync(CancellationToken ct)
    {
        PicaImageItem? item = _currentItem;

        if (item is null)
        {
            return;
        }

        ImageChannel? channel = _channelSelection.SelectedChannel;

        if (channel is not null)
        {
            bool isChannelReady =
                await WaitForCurrentImagePresentationAsync(ct);
            Bitmap? bitmap = _bitmap;

            if (!isChannelReady || (bitmap is null))
            {
                return;
            }

            await _imageOperations.SaveBitmapAsync(
                StorageProvider,
                bitmap,
                channel.CreateFileName(item.FileName),
                ct);
            _logger.LogInformation(
                "Completed save-as for channel {Channel} of Pica image {ItemId}",
                channel.Code,
                item.Id);
            return;
        }

        await _imageOperations.SaveCurrentAsync(StorageProvider, item, ct);
        _logger.LogInformation("Completed save-as for Pica image {ItemId}", item.Id);
    }

    private async Task SaveSelectionAsAndCloseAsync(CancellationToken ct)
    {
        await RunExclusiveImageOperationAsync(SaveSelectionAsCoreAsync, ct);
    }

    private async Task SaveSelectionAsCoreAsync(CancellationToken ct)
    {
        await RunWithFullResolutionSelectionAsync(
            async (bitmap, operationCt) =>
            {
                await _imageOperations.SaveSelectionAsync(
                    StorageProvider,
                    bitmap,
                    CancelSelection,
                    operationCt);
                _logger.LogInformation(
                    "Completed save-as for a Pica selection sized {Width}x{Height}",
                    bitmap.PixelSize.Width,
                    bitmap.PixelSize.Height);
            },
            ct);
    }

    private async Task RunWithFullResolutionSelectionAsync(
        Func<Bitmap, CancellationToken, Task> operation,
        CancellationToken ct)
    {
        bool isFullResolutionReady =
            await WaitForCurrentImagePresentationAsync(ct);

        if (!isFullResolutionReady)
        {
            return;
        }

        using Bitmap? bitmap = CreateSelectedBitmapOrDefault();

        if (bitmap is null)
        {
            return;
        }

        await operation(bitmap, ct);
    }

    private async Task<string?> GetOpenWithFilePathAsync(
        OpenWithTarget target,
        CancellationToken ct)
    {
        if (target == OpenWithTarget.CurrentImage)
        {
            ImageChannel? channel = _channelSelection.SelectedChannel;

            if (channel is null)
            {
                return _currentItem?.FilePath;
            }

            PreparedClipboardImage? channelImage =
                await PrepareCurrentChannelImageAsync(ct);

            if (channelImage is null)
            {
                return null;
            }

            string channelFilePath =
                _temporaryImageFileStore.CreateChannelFilePath(channel);
            await _temporaryImageFileStore.SaveAsync(
                channelFilePath,
                channelImage,
                ct);

            return channelFilePath;
        }

        bool isFullResolutionReady =
            await WaitForCurrentImagePresentationAsync(ct);

        if (!isFullResolutionReady)
        {
            return null;
        }

        PreparedClipboardImage? image = await GetPreparedSelectionClipboardImageAsync(ct);

        if (image is null)
        {
            return null;
        }

        string filePath = _temporaryImageFileStore.CreateSelectionFilePath();
        await _temporaryImageFileStore.SaveAsync(filePath, image, ct);

        return filePath;
    }

    private async Task<PreparedClipboardImage?> PrepareCurrentChannelImageAsync(
        CancellationToken ct)
    {
        if (!_channelSelection.IsActive)
        {
            return null;
        }

        bool isChannelReady = await WaitForCurrentImagePresentationAsync(ct);
        Bitmap? bitmap = _bitmap;

        if (!isChannelReady || (bitmap is null))
        {
            return null;
        }

        return await _clipboardImagePreparer.PrepareImageAsync(bitmap, ct);
    }

    private void HideOpenWithAfterAction(OpenWithTarget target)
    {
        if (target == OpenWithTarget.CurrentImage)
        {
            HideContextMenu();
            return;
        }

        HideOpenWithSubmenu();
    }

    private async Task RunExclusiveImageOperationAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (_isImageOperationRunning)
        {
            return;
        }

        _isImageOperationRunning = true;
        IsHitTestVisible = false;

        try
        {
            await operation(ct);
        }
        finally
        {
            IsHitTestVisible = true;
            _isImageOperationRunning = false;
        }
    }
}
