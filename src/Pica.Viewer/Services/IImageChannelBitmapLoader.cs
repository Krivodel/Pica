using Avalonia.Media.Imaging;

namespace Pica.Viewer.Services;

internal interface IImageChannelBitmapLoader
{
    Task<bool> ReadHasAlphaAsync(
        string fullPath,
        CancellationToken ct);

    Task<Bitmap> LoadAsync(
        Bitmap sourceBitmap,
        ImageChannel channel,
        CancellationToken ct);
}
