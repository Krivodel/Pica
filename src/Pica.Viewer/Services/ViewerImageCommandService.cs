using Avalonia;
using Avalonia.Media.Imaging;

using Pica.Protocol;

namespace Pica.Viewer.Services;

internal sealed class ViewerImageCommandService :
    IViewerImageCommandService,
    IDisposable
{
    public string? PreparedOpenWithFilePath { get; private set; }

    public event EventHandler? PreparedSelectionSaved;

    private readonly ImageViewerSession _session;
    private readonly ImagePresentationController _presentation;
    private readonly IImagePresentationReadiness _presentationReadiness;
    private readonly ViewerImageOperations _imageOperations;
    private readonly ClipboardImagePreparer _clipboardImagePreparer;
    private readonly ITemporaryImageFileStore _temporaryImageFileStore;
    private bool _disposed;

    internal ViewerImageCommandService(
        ImageViewerSession session,
        ImagePresentationController presentation,
        IImagePresentationReadiness presentationReadiness,
        ViewerImageOperations imageOperations,
        ClipboardImagePreparer clipboardImagePreparer,
        ITemporaryImageFileStore temporaryImageFileStore)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _presentation = presentation
            ?? throw new ArgumentNullException(nameof(presentation));
        _presentationReadiness = presentationReadiness
            ?? throw new ArgumentNullException(nameof(presentationReadiness));
        _imageOperations = imageOperations
            ?? throw new ArgumentNullException(nameof(imageOperations));
        _clipboardImagePreparer = clipboardImagePreparer
            ?? throw new ArgumentNullException(nameof(clipboardImagePreparer));
        _temporaryImageFileStore = temporaryImageFileStore
            ?? throw new ArgumentNullException(nameof(temporaryImageFileStore));
    }

    public async Task CopyCurrentAsync(
        CancellationToken ct)
    {
        await _presentationReadiness
            .WaitAsync(ct)
            .ConfigureAwait(false);

        if (!_presentationReadiness.IsReady)
        {
            return;
        }

        PicaImageItem? item = _presentation.CurrentItem;

        if (item is null)
        {
            return;
        }

        ImageChannel? channel = _session.SelectedChannel;
        using ImagePresentationBitmapLease? bitmapLease =
            _presentation.AcquireDisplayedBitmap(channel);

        if (bitmapLease is null)
        {
            return;
        }

        if (channel is null)
        {
            await _imageOperations.CopyFileAsync(
                item,
                bitmapLease.Bitmap,
                ct).ConfigureAwait(false);
            return;
        }

        PreparedClipboardImage preparedImage =
            await PrepareCurrentChannelImageAsync(
                bitmapLease.Bitmap,
                ct)
                .ConfigureAwait(false);
        await _imageOperations.CopyPreparedImageAsync(
            preparedImage,
            ct).ConfigureAwait(false);
    }

    public async Task CopyPreparedImageAsync(
        PreparedClipboardImage image,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(image);

        await _imageOperations
            .CopyPreparedImageAsync(image, ct)
            .ConfigureAwait(false);
    }

    public async Task<PreparedClipboardImage?> PrepareSelectionAsync(
        ImagePixelSelection selection,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ImageChannel? channel = _session.SelectedChannel;
        using ImagePresentationBitmapLease? bitmapLease =
            _presentation.AcquireDisplayedBitmap(channel);

        if (bitmapLease is null)
        {
            return null;
        }

        Bitmap sourceBitmap = bitmapLease.Bitmap;
        PixelRect sourceRect = new(
            selection.X,
            selection.Y,
            selection.Width,
            selection.Height);
        PixelRect? normalizedSourceRect =
            BitmapPixelCopy.NormalizeSourceRect(
                sourceBitmap.PixelSize,
                sourceRect);

        if (normalizedSourceRect is not { } validSourceRect)
        {
            return null;
        }

        using RenderTargetBitmap bitmap =
            BitmapPixelCopy.CreateRenderedCrop(
                sourceBitmap,
                validSourceRect);

        return await _clipboardImagePreparer
            .PrepareImageAsync(bitmap, ct)
            .ConfigureAwait(false);
    }

    public async Task DispatchCurrentAsync(
        PicaActionDefinition action,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (_session.IsChannelModeActive)
        {
            await _presentationReadiness
                .WaitAsync(ct)
                .ConfigureAwait(false);
        }

        ImageChannel? channel = _session.SelectedChannel;

        if ((channel is not null)
            && !_presentationReadiness.IsReady)
        {
            return;
        }

        PicaImageItem? item = _presentation.CurrentItem;

        if (item is null)
        {
            return;
        }

        if (channel is null)
        {
            await _imageOperations
                .DispatchCurrentAsync(action, item, ct)
                .ConfigureAwait(false);
            return;
        }

        using ImagePresentationBitmapLease? bitmapLease =
            _presentation.AcquireDisplayedBitmap(channel);

        if (bitmapLease is null)
        {
            return;
        }

        await _imageOperations.DispatchBitmapAsync(
            action,
            item,
            bitmapLease.Bitmap,
            ImageChannelFileName.Create(channel, item.FileName),
            ct).ConfigureAwait(false);
    }

    public async Task DispatchPreparedSelectionAsync(
        PicaActionDefinition action,
        PicaImageItem item,
        PreparedClipboardImage image,
        CancellationToken ct)
    {
        await _imageOperations.DispatchPreparedSelectionAsync(
            action,
            item,
            image,
            ct).ConfigureAwait(false);
    }

    public async Task SaveCurrentAsync(
        CancellationToken ct)
    {
        if (_session.IsChannelModeActive)
        {
            await _presentationReadiness
                .WaitAsync(ct)
                .ConfigureAwait(false);
        }

        ImageChannel? channel = _session.SelectedChannel;

        if ((channel is not null)
            && !_presentationReadiness.IsReady)
        {
            return;
        }

        PicaImageItem? item = _presentation.CurrentItem;

        if (item is null)
        {
            return;
        }

        if (channel is null)
        {
            await _imageOperations.SaveCurrentAsync(
                item,
                ct).ConfigureAwait(false);
            return;
        }

        using ImagePresentationBitmapLease? bitmapLease =
            _presentation.AcquireDisplayedBitmap(channel);

        if (bitmapLease is not null)
        {
            await _imageOperations.SaveBitmapAsync(
                bitmapLease.Bitmap,
                ImageChannelFileName.Create(channel, item.FileName),
                ct).ConfigureAwait(false);
        }
    }

    public async Task SavePreparedSelectionAsync(
        PreparedClipboardImage image,
        CancellationToken ct)
    {
        await _imageOperations.SavePreparedSelectionAsync(
            image,
            OnPreparedSelectionSaved,
            ct).ConfigureAwait(false);
    }

    public string GetCurrentOpenWithAssociationFilePath()
    {
        PicaImageItem item = _presentation.CurrentItem
            ?? throw new InvalidOperationException(
                "An image must be selected before opening it with another application.");
        ImageChannel? channel = _session.SelectedChannel;

        return channel is null
            ? item.FilePath
            : ImageChannelFileName.Create(channel, item.FileName);
    }

    public async Task PrepareCurrentOpenWithFileAsync(CancellationToken ct)
    {
        ResetOpenWithPreparation();

        if (_session.IsChannelModeActive)
        {
            await _presentationReadiness
                .WaitAsync(ct)
                .ConfigureAwait(false);
        }

        ImageChannel? channel = _session.SelectedChannel;

        if ((channel is not null)
            && !_presentationReadiness.IsReady)
        {
            return;
        }

        PicaImageItem? item = _presentation.CurrentItem;

        if (item is null)
        {
            return;
        }

        if (channel is null)
        {
            PreparedOpenWithFilePath = item.FilePath;
            return;
        }

        using ImagePresentationBitmapLease? bitmapLease =
            _presentation.AcquireDisplayedBitmap(channel);

        if (bitmapLease is null)
        {
            return;
        }

        PreparedClipboardImage image =
            await PrepareCurrentChannelImageAsync(
                bitmapLease.Bitmap,
                ct)
                .ConfigureAwait(false);

        string filePath =
            _temporaryImageFileStore.CreateChannelFilePath(channel);
        await _temporaryImageFileStore.SaveAsync(
            filePath,
            image,
            ct).ConfigureAwait(false);
        PreparedOpenWithFilePath = filePath;
    }

    public async Task PrepareSelectionOpenWithFileAsync(
        PreparedClipboardImage image,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(image);
        ResetOpenWithPreparation();
        string filePath = _temporaryImageFileStore.CreateSelectionFilePath();
        await _temporaryImageFileStore
            .SaveAsync(filePath, image, ct)
            .ConfigureAwait(false);
        PreparedOpenWithFilePath = filePath;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _temporaryImageFileStore.Dispose();
    }

    private void ResetOpenWithPreparation()
    {
        PreparedOpenWithFilePath = null;
    }

    private async Task<PreparedClipboardImage> PrepareCurrentChannelImageAsync(
        Bitmap bitmap,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        return await _clipboardImagePreparer
            .PrepareImageAsync(bitmap, ct)
            .ConfigureAwait(false);
    }

    private void OnPreparedSelectionSaved()
    {
        PreparedSelectionSaved?.Invoke(this, EventArgs.Empty);
    }
}
