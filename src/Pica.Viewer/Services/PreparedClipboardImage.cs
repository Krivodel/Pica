using Avalonia;

namespace Pica.Viewer.Services;

internal sealed record PreparedClipboardImage(
    PixelSize PixelSize,
    int RowBytes,
    byte[] BgraPixels,
    byte[] PngContent)
    : PreparedClipboardBitmap(PixelSize, RowBytes, BgraPixels);
