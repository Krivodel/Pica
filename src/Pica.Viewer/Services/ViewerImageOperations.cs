using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;

using Pica.Protocol;
using Pica.Viewer.Resources;

namespace Pica.Viewer.Services;

internal sealed class ViewerImageOperations
{
    private readonly IViewerClipboardWriter _clipboardImageWriter;
    private readonly IViewerFilePickerService _filePickerService;
    private readonly IImageFormatRegistry _formatRegistry;
    private readonly PngImageEncoder _pngImageEncoder;
    private readonly IViewerActionDispatcher _actionDispatcher;

    internal ViewerImageOperations(
        IViewerClipboardWriter clipboardImageWriter,
        IViewerFilePickerService filePickerService,
        IImageFormatRegistry formatRegistry,
        PngImageEncoder pngImageEncoder,
        IViewerActionDispatcher actionDispatcher)
    {
        _clipboardImageWriter = clipboardImageWriter
            ?? throw new ArgumentNullException(nameof(clipboardImageWriter));
        _filePickerService = filePickerService
            ?? throw new ArgumentNullException(nameof(filePickerService));
        _formatRegistry = formatRegistry ?? throw new ArgumentNullException(nameof(formatRegistry));
        _pngImageEncoder = pngImageEncoder
            ?? throw new ArgumentNullException(nameof(pngImageEncoder));
        _actionDispatcher = actionDispatcher
            ?? throw new ArgumentNullException(nameof(actionDispatcher));
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

        IStorageFile? file = await _filePickerService
            .GetFileFromPathAsync(item.FilePath, ct)
            .ConfigureAwait(false);

        if (file is null)
        {
            return;
        }

        if (bitmap is null)
        {
            await _clipboardImageWriter
                .SetFileAsync(file, ct)
                .ConfigureAwait(false);
            return;
        }

        await _clipboardImageWriter
            .SetFileWithImageAsync(file, bitmap, ct)
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
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(bitmap);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

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

    internal async Task SaveCurrentAsync(
        PicaImageItem item,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(item);

        await SaveImageAsync(
            () => CreateCurrentImageSavePicker(item),
            currentCt => File.ReadAllBytesAsync(item.FilePath, currentCt),
            SaveContentPreparationTiming.BeforeOpeningDestination,
            ct).ConfigureAwait(false);
    }

    internal async Task SavePreparedSelectionAsync(
        PreparedClipboardImage image,
        Action saved,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(saved);
        (string suggestedFileName, FilePickerFileType fileType) =
            CreatePngSavePicker(PicaImageFormats.SelectionFileName);
        IStorageFile? destination = await ShowSaveFilePickerAsync(
            suggestedFileName,
            fileType,
            ct).ConfigureAwait(false);

        if (destination is null)
        {
            return;
        }

        await WriteImageAsync(
            destination,
            currentCt => Task.FromResult(image.PngContent),
            image.PngContent,
            ct).ConfigureAwait(false);
        saved();
    }

    internal async Task SaveBitmapAsync(
        Bitmap bitmap,
        string suggestedFileName,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        ArgumentException.ThrowIfNullOrWhiteSpace(suggestedFileName);

        await SaveImageAsync(
            () => CreatePngSavePicker(suggestedFileName),
            currentCt => _pngImageEncoder.EncodeAsync(bitmap, currentCt),
            SaveContentPreparationTiming.AfterOpeningDestination,
            ct).ConfigureAwait(false);
    }

    private static (string SuggestedFileName, FilePickerFileType FileType) CreatePngSavePicker(
        string suggestedFileName)
    {
        FilePickerFileType fileType = new("PNG")
        {
            MimeTypes = [PicaImageFormats.PngContentType],
            Patterns = ["*" + PicaImageFormats.PngExtension]
        };

        return (suggestedFileName, fileType);
    }

    private static void ClearWritableStream(Stream stream)
    {
        if (stream.CanSeek)
        {
            stream.SetLength(0);
        }
    }

    private static async Task WriteImageAsync(
        IStorageFile destination,
        Func<CancellationToken, Task<byte[]>> createContent,
        byte[]? preparedContent,
        CancellationToken ct)
    {
        await using Stream target = await destination
            .OpenWriteAsync()
            .ConfigureAwait(false);
        ClearWritableStream(target);
        byte[] content = preparedContent
            ?? await createContent(ct).ConfigureAwait(false);
        await target.WriteAsync(content, ct).ConfigureAwait(false);
    }

    private async Task SaveImageAsync(
        Func<(string SuggestedFileName, FilePickerFileType FileType)> createPicker,
        Func<CancellationToken, Task<byte[]>> createContent,
        SaveContentPreparationTiming preparationTiming,
        CancellationToken ct)
    {
        (string suggestedFileName, FilePickerFileType fileType) = createPicker();
        IStorageFile? destination = await ShowSaveFilePickerAsync(
            suggestedFileName,
            fileType,
            ct).ConfigureAwait(false);

        if (destination is null)
        {
            return;
        }

        byte[]? preparedContent = preparationTiming
            == SaveContentPreparationTiming.BeforeOpeningDestination
            ? await createContent(ct).ConfigureAwait(false)
            : null;
        await WriteImageAsync(
            destination,
            createContent,
            preparedContent,
            ct).ConfigureAwait(false);
    }

    private (string SuggestedFileName, FilePickerFileType FileType) CreateCurrentImageSavePicker(
        PicaImageItem item)
    {
        string fileName = item.FileName;
        string extension = Path.GetExtension(fileName);

        return (fileName, CreateImageFilePickerFileType(extension, fileName));
    }

    private async Task<IStorageFile?> ShowSaveFilePickerAsync(
        string suggestedFileName,
        FilePickerFileType fileType,
        CancellationToken ct)
    {
        FilePickerSaveOptions options = new()
        {
            FileTypeChoices = [fileType],
            SuggestedFileName = suggestedFileName,
            Title = ViewerUiStrings.SaveAs
        };

        return await _filePickerService
            .SelectSaveDestinationAsync(options, ct)
            .ConfigureAwait(false);
    }

    private FilePickerFileType CreateImageFilePickerFileType(
        string extension,
        string fallbackFileName)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = _formatRegistry.GetExtension(fallbackFileName);
        }

        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = PicaImageFormats.PngExtension;
        }

        string normalizedExtension = extension.StartsWith('.')
            ? extension
            : "." + extension;
        string label = normalizedExtension.TrimStart('.').ToUpperInvariant();

        return new FilePickerFileType(label)
        {
            Patterns = ["*" + normalizedExtension]
        };
    }

    private enum SaveContentPreparationTiming
    {
        BeforeOpeningDestination,
        AfterOpeningDestination
    }
}
