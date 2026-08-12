namespace Pica.Viewer.Services;

internal sealed record ApngAnimationData(
    int CanvasWidth,
    int CanvasHeight,
    uint AnimationIterations,
    byte[] ImageHeaderData,
    IReadOnlyList<byte[]> SharedChunks,
    IReadOnlyList<ApngFrameData> Frames);
