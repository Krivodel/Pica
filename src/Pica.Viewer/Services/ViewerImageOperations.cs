using Avalonia.Media.Imaging;

using Pica.Protocol;

namespace Pica.Viewer.Services;

internal sealed class ViewerImageOperations
{
    internal event EventHandler? SaveWritingStarted;

    private readonly IViewerClipboardWriter _clipboardImageWriter;
    private readonly ViewerImageSaveService _imageSaveService;
    private readonly PngImageEncoder _pngImageEncoder;
    private readonly IViewerActionDispatcher _actionDispatcher;
    private readonly ViewerClipboardFileCopy _clipboardFileCopy;

    internal ViewerImageOperations(
        IViewerClipboardWriter clipboardImageWriter,
        IViewerFilePickerService filePickerService,
        IImageFormatRegistry formatRegistry,
        PngImageEncoder pngImageEncoder,
        IViewerActionDispatcher actionDispatcher)
    {
        _clipboardImageWriter = clipboardImageWriter
            ?? throw new ArgumentNullException(nameof(clipboardImageWriter));
        _imageSaveService = new ViewerImageSaveService(
            filePickerService,
            formatRegistry);
        _imageSaveService.SaveWritingStarted += OnSaveWritingStarted;
        _pngImageEncoder = pngImageEncoder
            ?? throw new ArgumentNullException(nameof(pngImageEncoder));
        _actionDispatcher = actionDispatcher
            ?? throw new ArgumentNullException(nameof(actionDispatcher));
        _clipboardFileCopy = new ViewerClipboardFileCopy(
            filePickerService,
            _clipboardImageWriter);
    }

    internal async Task CopyPreparedImageAsync(
        PreparedClipboardImage image,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(image);

        await _clipboardImageWriter
            .SetPreparedImageAsync(image, ct)
            .ConfigureAwait(false);
    }

    internal async Task CopyFileAsync(
        PicaImageItem item,
        Bitmap? bitmap,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(item);

        await _clipboardFileCopy
            .CopyAsync(item.FilePath, bitmap, ct)
            .ConfigureAwait(false);
    }

    internal async Task DispatchCurrentAsync(
        PicaActionDefinition action,
        PicaImageItem item,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(item);

        await _actionDispatcher
            .DispatchCurrentImageAsync(action, item, ct)
            .ConfigureAwait(false);
    }

    internal async Task DispatchPreparedSelectionAsync(
        PicaActionDefinition action,
        PicaImageItem item,
        PreparedClipboardImage image,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(image);

        await _actionDispatcher.DispatchSelectionAsync(
            action,
            item,
            image.PngContent,
            ct).ConfigureAwait(false);
    }

    internal async Task DispatchBitmapAsync(
        PicaActionDefinition action,
        PicaImageItem item,
        Bitmap bitmap,
        string fileName,
        bool allowDirectDispatch,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(bitmap);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        if (allowDirectDispatch
            && _actionDispatcher.CanDispatchBitmapWithoutEncoding(action, item))
        {
            await _actionDispatcher.DispatchBitmapAsync(
                action,
                item,
                bitmap,
                fileName,
                ct).ConfigureAwait(false);
        }
        else
        {
            byte[] pngContent = await _pngImageEncoder
                .EncodeAsync(bitmap, ct)
                .ConfigureAwait(false);
            await _actionDispatcher.DispatchDerivedImageAsync(
                action,
                item,
                fileName,
                pngContent,
                ct).ConfigureAwait(false);
        }
    }

    internal async Task SaveCurrentAsync(
        PicaImageItem item,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(item);

        await _imageSaveService.SaveFileAsync(
            item.FilePath,
            item.FileName,
            ct).ConfigureAwait(false);
    }

    internal async Task SavePreparedSelectionAsync(
        PreparedClipboardImage image,
        Action saved,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(saved);
        bool wasSaved = await _imageSaveService.SaveImageAsync(
            PicaImageFormats.SelectionFileName,
            PicaImageFormats.PngExtension,
            currentCt => Task.FromResult(image.PngContent),
            ct).ConfigureAwait(false);

        if (wasSaved)
        {
            saved();
        }
    }

    internal async Task SaveBitmapAsync(
        Bitmap bitmap,
        string suggestedFileName,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        ArgumentException.ThrowIfNullOrWhiteSpace(suggestedFileName);

        await _imageSaveService.SaveImageAsync(
            Path.ChangeExtension(suggestedFileName, PicaImageFormats.PngExtension),
            PicaImageFormats.PngExtension,
            currentCt => _pngImageEncoder.EncodeAsync(bitmap, currentCt),
            ct).ConfigureAwait(false);
    }

    private void OnSaveWritingStarted(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        SaveWritingStarted?.Invoke(this, eventArgs);
    }
}
