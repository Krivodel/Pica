using Avalonia.Media.Imaging;

namespace Pica.Viewer.Services;

internal sealed record DecodedImageFrame(
    Bitmap Bitmap,
    TimeSpan Duration);
