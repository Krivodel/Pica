using Microsoft.Extensions.Logging;

using Avalonia.Media.Imaging;
using SukiUI.Controls;

using Pica.Protocol;
using Pica.Viewer.Services;

namespace Pica.Viewer.Views;

public sealed partial class ImageViewerWindow : SukiWindow
{
    private static void DisposeBitmapAfterTask(
        Bitmap bitmap,
        Task? activeTask)
    {
        if ((activeTask is null) || activeTask.IsCompleted)
        {
            bitmap.Dispose();
            return;
        }

        _ = DisposeBitmapAfterTaskAsync(bitmap, activeTask);
    }

    private static async Task DisposeBitmapAfterTaskAsync(
        Bitmap bitmap,
        Task activeTask)
    {
        try
        {
            await activeTask;
        }
        finally
        {
            bitmap.Dispose();
        }
    }

    private void ToggleChannelMode()
    {
        if (_channelSelection.IsActive)
        {
            ExitChannelMode();
            return;
        }

        EnterChannelMode();
    }

    private void EnterChannelMode()
    {
        if (_channelSelection.IsActive)
        {
            return;
        }

        _channelSelection.Enter();
        _view.UpdateImageModeMenuState(ViewerImageMode.Channels);
        UpdateSelectedImageInformation();
        StartSelectedChannelLoad();
    }

    private void ExitChannelMode()
    {
        _channelSelection.Exit();
        _view.UpdateImageModeMenuState(ViewerImageMode.Main);
        CancelPendingChannelLoad();
        ShowSourceBitmap();
        UpdateSelectedImageInformation();
    }

    private void NavigateChannel(int direction)
    {
        _channelSelection.Navigate(direction);
        UpdateSelectedImageInformation();
        StartSelectedChannelLoad();
    }

    private void StartSelectedChannelLoad()
    {
        ImageChannel? channel = _channelSelection.SelectedChannel;
        Bitmap? sourceBitmap = _sourceBitmap;
        PicaImageItem? item = _currentItem;

        if (!_isFullResolutionImageReady
            || (channel is null)
            || (sourceBitmap is null)
            || (item is null))
        {
            return;
        }

        CancelPendingChannelLoad();
        CancellationTokenSource cancellation = new();
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
        CancellationTokenSource cancellation)
    {
        Bitmap? channelBitmap = null;
        CancellationToken ct = cancellation.Token;

        try
        {
            if (!_channelSelection.IsAvailabilityKnown)
            {
                bool hasAlpha = await _imageChannelBitmapLoader
                    .ReadHasAlphaAsync(item.FilePath, ct);

                if (!CanApplyChannelLoad(sourceBitmap, channel, loadId, ct))
                {
                    return;
                }

                _channelSelection.SetHasAlpha(hasAlpha);
            }

            channelBitmap = await _imageChannelBitmapLoader.LoadAsync(
                sourceBitmap,
                channel,
                ct);

            if (!CanApplyChannelLoad(sourceBitmap, channel, loadId, ct))
            {
                return;
            }

            ApplyChannelBitmap(channelBitmap, channel);
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

            if (ReferenceEquals(_channelLoadCancellation, cancellation))
            {
                _channelLoadCancellation = null;
                _activeChannelLoadTask = null;
            }

            cancellation.Dispose();
        }
    }

    private bool CanApplyChannelLoad(
        Bitmap sourceBitmap,
        ImageChannel channel,
        long loadId,
        CancellationToken ct)
    {
        return !ct.IsCancellationRequested
            && (loadId == _channelLoadId)
            && _channelSelection.IsActive
            && ReferenceEquals(sourceBitmap, _sourceBitmap)
            && Equals(channel, _channelSelection.SelectedChannel);
    }

    private void ApplyChannelBitmap(
        Bitmap channelBitmap,
        ImageChannel channel)
    {
        Bitmap? previousChannelBitmap = _channelBitmap;
        _channelBitmap = channelBitmap;
        _displayedChannel = channel;
        _bitmap = channelBitmap;
        _view.Image.Source = channelBitmap;
        previousChannelBitmap?.Dispose();
        RefreshSelectionAfterDisplayedBitmapChange();
    }

    private void ShowSourceBitmap()
    {
        Bitmap? channelBitmap = _channelBitmap;
        _channelBitmap = null;
        _displayedChannel = null;
        _bitmap = _sourceBitmap;
        _view.Image.Source = _sourceBitmap;
        channelBitmap?.Dispose();
        RefreshSelectionAfterDisplayedBitmapChange();
    }

    private void RefreshSelectionAfterDisplayedBitmapChange()
    {
        CancelSelectionClipboardPreparation();

        if (_isSelectionActive)
        {
            ScheduleSelectionClipboardPreparation();
        }
    }

    private async Task<bool> WaitForCurrentImagePresentationAsync(
        CancellationToken ct)
    {
        bool isFullResolutionReady = await WaitForFullResolutionImageAsync(ct);

        if (!isFullResolutionReady)
        {
            return false;
        }

        if (!_channelSelection.IsActive)
        {
            return true;
        }

        ImageChannel? selectedChannel = _channelSelection.SelectedChannel;

        if ((_channelBitmap is not null)
            && Equals(_displayedChannel, selectedChannel))
        {
            return true;
        }

        if (_activeChannelLoadTask is null)
        {
            StartSelectedChannelLoad();
        }

        Task? channelLoadTask = _activeChannelLoadTask;

        if (channelLoadTask is not null)
        {
            await channelLoadTask.WaitAsync(ct);
        }

        return _channelBitmap is not null
            && Equals(_displayedChannel, _channelSelection.SelectedChannel);
    }

    private void CancelPendingChannelLoad()
    {
        _channelLoadId++;
        CancellationTokenSource? cancellation = _channelLoadCancellation;
        _channelLoadCancellation = null;
        _activeChannelLoadTask = null;
        cancellation?.Cancel();
    }

    private void ReplaceSourceBitmap(Bitmap bitmap)
    {
        Task? activeChannelLoadTask = _activeChannelLoadTask;
        CancelPendingChannelLoad();
        Bitmap? previousChannelBitmap = _channelBitmap;
        Bitmap? previousSourceBitmap = _sourceBitmap;
        _channelBitmap = null;
        _displayedChannel = null;
        _sourceBitmap = bitmap;
        _bitmap = bitmap;
        _view.Image.Source = bitmap;
        previousChannelBitmap?.Dispose();

        if (previousSourceBitmap is not null)
        {
            DisposeBitmapAfterTask(previousSourceBitmap, activeChannelLoadTask);
        }
    }

    private void ReleaseImageBitmaps()
    {
        Task? activeChannelLoadTask = _activeChannelLoadTask;
        CancelPendingChannelLoad();
        Bitmap? channelBitmap = _channelBitmap;
        Bitmap? sourceBitmap = _sourceBitmap;
        _channelBitmap = null;
        _displayedChannel = null;
        _sourceBitmap = null;
        _bitmap = null;
        _view.Image.Source = null;
        channelBitmap?.Dispose();

        if (sourceBitmap is not null)
        {
            DisposeBitmapAfterTask(sourceBitmap, activeChannelLoadTask);
        }
    }
}
