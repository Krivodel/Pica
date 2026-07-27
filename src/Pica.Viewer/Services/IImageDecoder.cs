using Avalonia;
using Avalonia.Media.Imaging;

namespace Pica.Viewer.Services;

internal interface IImageDecoder
{
    PixelSize ReadPixelSize(Stream sourceStream, CancellationToken ct);

    Bitmap Decode(Stream sourceStream, CancellationToken ct);

    Bitmap DecodeToWidth(Stream sourceStream, int width, CancellationToken ct);
}
