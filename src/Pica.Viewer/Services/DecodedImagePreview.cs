using Avalonia;
using Avalonia.Media.Imaging;

namespace Pica.Viewer.Services;

internal sealed record DecodedImagePreview(
    Bitmap Bitmap,
    PixelSize SourcePixelSize);
