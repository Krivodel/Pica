using Avalonia.Platform.Storage;
using ImageMagick;

using Pica.Viewer.Resources;

namespace Pica.Viewer.Services;

internal sealed class ViewerImageSaveService
{
    internal event EventHandler? SaveWritingStarted;

    private readonly IViewerFilePickerService _filePickerService;
    private readonly IImageFormatRegistry _formatRegistry;

    internal ViewerImageSaveService(
        IViewerFilePickerService filePickerService,
        IImageFormatRegistry formatRegistry)
    {
        _filePickerService = filePickerService
            ?? throw new ArgumentNullException(nameof(filePickerService));
        _formatRegistry = formatRegistry
            ?? throw new ArgumentNullException(nameof(formatRegistry));
    }

    internal Task<bool> SaveFileAsync(
        string filePath,
        string suggestedFileName,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(suggestedFileName);

        return SaveImageAsync(
            suggestedFileName,
            GetFileExtension(suggestedFileName),
            currentCt => File.ReadAllBytesAsync(filePath, currentCt),
            ct);
    }

    internal async Task<bool> SaveImageAsync(
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

        SaveWritingStarted?.Invoke(this, EventArgs.Empty);
        byte[] sourceContent = await createContent(ct).ConfigureAwait(false);
        byte[] content = await PrepareSaveContentAsync(
            sourceContent,
            sourceExtension,
            destination,
            ct).ConfigureAwait(false);
        await WriteImageAsync(destination, content, ct).ConfigureAwait(false);

        return true;
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
