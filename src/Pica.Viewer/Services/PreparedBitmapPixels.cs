using Avalonia;

namespace Pica.Viewer.Services;

internal record PreparedBitmapPixels(
    PixelSize PixelSize,
    int RowBytes,
    byte[] BgraPixels);
