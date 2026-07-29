namespace Pica.Viewer.Services;

internal sealed record PreparedClipboardImage(
    ImageDimensions Dimensions,
    int RowBytes,
    byte[] BgraPixels,
    byte[] PngContent)
    : PreparedBitmapPixels(Dimensions, RowBytes, BgraPixels);
