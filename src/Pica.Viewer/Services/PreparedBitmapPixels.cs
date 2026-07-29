namespace Pica.Viewer.Services;

internal record PreparedBitmapPixels(
    ImageDimensions Dimensions,
    int RowBytes,
    byte[] BgraPixels);
