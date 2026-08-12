namespace Pica.Viewer.Services;

internal sealed record IsoBmffAnimationMetadata(
    IReadOnlyList<TimeSpan> FrameDurations,
    uint AnimationIterations);
