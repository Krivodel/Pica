using Avalonia;

namespace Pica.Viewer.Services;

internal record PreparedClipboardBitmap(
    PixelSize PixelSize,
    int RowBytes,
    byte[] BgraPixels);
