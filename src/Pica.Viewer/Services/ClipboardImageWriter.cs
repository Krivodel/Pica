using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;

namespace Pica.Viewer.Services;

internal sealed class ClipboardImageWriter : IViewerClipboardWriter, IDisposable
{
    private readonly AvaloniaClipboardDataWriter _clipboardDataWriter;
    private readonly IPlatformClipboardImageWriter _platformImageWriter;

    public ClipboardImageWriter(
        AvaloniaClipboardDataWriter clipboardDataWriter,
        IPlatformClipboardImageWriter platformImageWriter)
    {
        _clipboardDataWriter = clipboardDataWriter
            ?? throw new ArgumentNullException(nameof(clipboardDataWriter));
        _platformImageWriter = platformImageWriter
            ?? throw new ArgumentNullException(nameof(platformImageWriter));
    }

    public void Attach(IClipboard clipboard)
    {
        _clipboardDataWriter.Attach(clipboard);
    }

    public async Task FlushAsync(CancellationToken ct)
    {
        await _clipboardDataWriter.FlushAsync(ct);
    }

    public void Dispose()
    {
        _clipboardDataWriter.Dispose();
    }

    async Task IViewerClipboardWriter.SetPreparedImageAsync(
        PreparedClipboardImage image,
        CancellationToken ct)
    {
        await _platformImageWriter.SetImageAsync(image, ct);
    }

    async Task IViewerClipboardWriter.SetFileAsync(IStorageFile file, CancellationToken ct)
    {
        await _clipboardDataWriter.SetFileAsync(file, ct);
    }

    async Task IViewerClipboardWriter.SetFileWithImageAsync(
        IStorageFile file,
        Bitmap bitmap,
        CancellationToken ct)
    {
        await _platformImageWriter.SetFileWithImageAsync(file, bitmap, ct);
    }
}
