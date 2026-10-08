using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;

namespace Pica.Viewer.Services;

internal interface IPlatformClipboardImageWriter
{
    Task SetImageAsync(PreparedBitmapPixels image, CancellationToken ct);
    Task SetFileWithImageAsync(IStorageFile file, Bitmap bitmap, CancellationToken ct);
}
