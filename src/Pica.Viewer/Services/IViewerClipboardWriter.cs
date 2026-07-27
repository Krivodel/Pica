using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;

namespace Pica.Viewer.Services;

internal interface IViewerClipboardWriter : IClipboardImageWriter
{
    Task SetPreparedImageAsync(PreparedClipboardImage image, CancellationToken ct);
    Task SetFileAsync(IStorageFile file, CancellationToken ct);
    Task SetFileWithImageAsync(IStorageFile file, Bitmap bitmap, CancellationToken ct);
}
