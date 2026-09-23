using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using ImageMagick;

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
        _filePickerService = filePickerService
            ?? throw new ArgumentNullException(nameof(filePickerService));
        _formatRegistry = formatRegistry ?? throw new ArgumentNullException(nameof(formatRegistry));
        _pngImageEncoder = pngImageEncoder
            ?? throw new ArgumentNullException(nameof(pngImageEncoder));
        _actionDispatcher = actionDispatcher
            ?? throw new ArgumentNullException(nameof(actionDispatcher));
        _clipboardFileCopy = new ViewerClipboardFileCopy(
            _filePickerService,
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
            item.FileName,
            GetFileExtension(item.FileName),
            currentCt => File.ReadAllBytesAsync(item.FilePath, currentCt),
            ct).ConfigureAwait(false);
    }

    internal async Task SavePreparedSelectionAsync(
        PreparedClipboardImage image,
        Action saved,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(saved);
        bool wasSaved = await SaveImageAsync(
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

        await SaveImageAsync(
            Path.ChangeExtension(suggestedFileName, PicaImageFormats.PngExtension),
            PicaImageFormats.PngExtension,
            currentCt => _pngImageEncoder.EncodeAsync(bitmap, currentCt),
            ct).ConfigureAwait(false);
    }

    private static string GetFileExtension(string fileName)
    {
        string extension = Path.GetExtension(fileName).ToLowerInvariant();

        return string.IsNullOrWhiteSpace(extension)
            ? PicaImageFormats.PngExtension
            : extension;
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
        byte[] content,
        CancellationToken ct)
    {
        await using Stream target = await destination
            .OpenWriteAsync()
            .ConfigureAwait(false);
        ClearWritableStream(target);
        await target.WriteAsync(content, ct).ConfigureAwait(false);
    }

    private static byte[] ConvertImage(
        byte[] sourceContent,
        MagickFormat? sourceReadFormat,
        MagickFormat format,
        bool supportsMultipleFrames,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using MagickImageCollection images = new();
        using MemoryStream source = new(sourceContent);
        MagickReadSettings? readSettings = sourceReadFormat is MagickFormat readFormat
            ? new MagickReadSettings { Format = readFormat }
            : null;
        MagickImageCollectionReader.Read(images, source, readSettings);
        using MemoryStream output = new();

        if (supportsMultipleFrames && (images.Count > 1))
        {
            images.Write(output, format);
        }
        else
        {
            images[0].Write(output, format);
        }

        ct.ThrowIfCancellationRequested();

        return output.ToArray();
    }

    private async Task<byte[]> PrepareSaveContentAsync(
        byte[] sourceContent,
        string sourceExtension,
        IStorageFile destination,
        CancellationToken ct)
    {
        string destinationExtension = GetFileExtension(destination.Name);

        if (string.Equals(
                sourceExtension,
                destinationExtension,
                StringComparison.OrdinalIgnoreCase))
        {
            return sourceContent;
        }

        if (!_formatRegistry.GetWritableExtensions().Contains(destinationExtension))
        {
            throw new NotSupportedException(
                $"The image format '{destinationExtension}' is not available for saving.");
        }

        IMagickFormatInfo? destinationFormat = MagickFormatInfo.Create(destination.Name);

        if (destinationFormat is not { SupportsWriting: true })
        {
            throw new NotSupportedException(
                $"The image format '{destinationExtension}' cannot be written.");
        }

        return await Task.Run(() => ConvertImage(
            sourceContent,
            _formatRegistry.GetMultiFrameReadFormat("image" + sourceExtension),
            destinationFormat.Format,
            destinationFormat.SupportsMultipleFrames,
            ct), ct).ConfigureAwait(false);
    }

    private async Task<bool> SaveImageAsync(
        string suggestedFileName,
        string sourceExtension,
        Func<CancellationToken, Task<byte[]>> createContent,
        CancellationToken ct)
    {
        IStorageFile? destination = await ShowSaveFilePickerAsync(
            suggestedFileName,
            sourceExtension,
            ct).ConfigureAwait(false);

        if (destination is null)
        {
            return false;
        }

        byte[] sourceContent = await createContent(ct).ConfigureAwait(false);
        byte[] content = await PrepareSaveContentAsync(
            sourceContent,
            sourceExtension,
            destination,
            ct).ConfigureAwait(false);
        await WriteImageAsync(destination, content, ct).ConfigureAwait(false);

        return true;
    }

    private async Task<IStorageFile?> ShowSaveFilePickerAsync(
        string suggestedFileName,
        string defaultExtension,
        CancellationToken ct)
    {
        IReadOnlyList<string> extensions = _formatRegistry.GetWritableExtensions();
        List<FilePickerFileType> fileTypes = new(extensions.Count + 1)
        {
            CreateImageFilePickerFileType(defaultExtension)
        };

        foreach (string extension in extensions)
        {
            if (!string.Equals(
                    extension,
                    defaultExtension,
                    StringComparison.OrdinalIgnoreCase))
            {
                fileTypes.Add(CreateImageFilePickerFileType(extension));
            }
        }

        FilePickerSaveOptions options = new()
        {
            FileTypeChoices = fileTypes,
            SuggestedFileType = fileTypes[0],
            SuggestedFileName = suggestedFileName,
            Title = ViewerUiStrings.SaveAs
        };

        return await _filePickerService
            .SelectSaveDestinationAsync(options, ct)
            .ConfigureAwait(false);
    }

    private FilePickerFileType CreateImageFilePickerFileType(string extension)
    {
        string normalizedExtension = extension.StartsWith('.')
            ? extension
            : "." + extension;
        string label = normalizedExtension.TrimStart('.').ToUpperInvariant();
        string fileName = "image" + normalizedExtension;
        string[]? mimeTypes = _formatRegistry.IsSupportedFileName(fileName)
            ? [_formatRegistry.GetContentType(fileName)]
            : null;

        return new FilePickerFileType(label)
        {
            MimeTypes = mimeTypes,
            Patterns = ["*" + normalizedExtension]
        };
    }
}
