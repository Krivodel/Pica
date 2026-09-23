using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;

namespace Pica.Viewer.Services;

internal sealed class ViewerClipboardFileCopy
{
    private readonly IViewerFilePickerService _filePickerService;
    private readonly IViewerClipboardWriter _clipboardWriter;

    internal ViewerClipboardFileCopy(
        IViewerFilePickerService filePickerService,
        IViewerClipboardWriter clipboardWriter)
    {
        _filePickerService = filePickerService
            ?? throw new ArgumentNullException(nameof(filePickerService));
        _clipboardWriter = clipboardWriter
            ?? throw new ArgumentNullException(nameof(clipboardWriter));
    }

    internal async Task CopyAsync(
        string filePath,
        Bitmap? bitmap,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        IStorageFile sourceFile = await _filePickerService
            .GetFileFromPathAsync(filePath, ct)
            .ConfigureAwait(false)
            ?? throw new FileNotFoundException(
                $"Cannot copy image because the storage provider did not resolve '{filePath}'.",
                filePath);

        if (bitmap is null)
        {
            await _clipboardWriter
                .SetFileAsync(sourceFile, ct)
                .ConfigureAwait(false);
        }
        else
        {
            await _clipboardWriter
                .SetFileWithImageAsync(sourceFile, bitmap, ct)
                .ConfigureAwait(false);
        }
    }
}
