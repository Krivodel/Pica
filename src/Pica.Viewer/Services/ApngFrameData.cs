namespace Pica.Viewer.Services;

internal sealed record ApngFrameData(
    int X,
    int Y,
    int Width,
    int Height,
    TimeSpan Duration,
    ApngDisposeOperation DisposeOperation,
    ApngBlendOperation BlendOperation,
    IReadOnlyList<byte[]> CompressedData);
