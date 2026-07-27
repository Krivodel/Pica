using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;

namespace Pica.Viewer.Services;

internal sealed class PngPlatformClipboardImageWriter : IPlatformClipboardImageWriter
{
    private readonly AvaloniaClipboardDataWriter _clipboardDataWriter;
    private readonly DataFormat<byte[]> _pngFormat;

    public PngPlatformClipboardImageWriter(
        AvaloniaClipboardDataWriter clipboardDataWriter,
        string formatIdentifier)
    {
        _clipboardDataWriter = clipboardDataWriter
            ?? throw new ArgumentNullException(nameof(clipboardDataWriter));
        _pngFormat = DataFormat.CreateBytesPlatformFormat(formatIdentifier);
    }

    public async Task SetImageAsync(PreparedClipboardImage image, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(image);

        await _clipboardDataWriter.SetBytesAsync(_pngFormat, image.PngContent, ct);
    }

    public async Task SetFileWithImageAsync(
        IStorageFile file,
        Bitmap bitmap,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(bitmap);

        await _clipboardDataWriter.SetFileAsync(file, ct);
    }
}
