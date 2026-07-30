using Avalonia.Media.Imaging;

namespace Pica.Viewer.Services;

internal interface IFullResolutionImageLoader
{
    Task<Bitmap> LoadAsync(string fullPath, CancellationToken ct);
}
