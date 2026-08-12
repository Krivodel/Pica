namespace Pica.Viewer.Services;

internal interface IProgressiveImageFrameReader : IDisposable
{
    int FrameCount { get; }
    uint AnimationIterations { get; }
    IReadOnlyList<TimeSpan> FrameDurations { get; }

    DecodedImageFrame ReadFrame(
        int frameIndex,
        CancellationToken ct);
}
