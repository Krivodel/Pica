namespace Pica.Viewer.Services;

internal sealed record MagickAnimationDecodingPolicy
{
    internal static MagickAnimationDecodingPolicy DependentFrameSequence { get; } =
        new(
            TimeSpan.FromSeconds(
                DependentSequencePlaybackRunwaySeconds),
            DependentSequenceInitialFrameFraction);

    private const double DependentSequenceInitialFrameFraction =
        0.4d;
    private const double DependentSequencePlaybackRunwaySeconds =
        4d;

    private readonly TimeSpan _minimumPlaybackRunway;
    private readonly double _minimumInitialFrameFraction;

    private MagickAnimationDecodingPolicy(
        TimeSpan minimumPlaybackRunway,
        double minimumInitialFrameFraction)
    {
        if (minimumPlaybackRunway <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumPlaybackRunway),
                minimumPlaybackRunway,
                "The minimum playback runway must be positive.");
        }

        if ((minimumInitialFrameFraction <= 0d)
            || (minimumInitialFrameFraction > 1d))
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumInitialFrameFraction),
                minimumInitialFrameFraction,
                "The minimum initial frame fraction must be between zero and one.");
        }

        _minimumPlaybackRunway = minimumPlaybackRunway;
        _minimumInitialFrameFraction =
            minimumInitialFrameFraction;
    }

    internal int GetInitialDecodeFrameCount(
        IReadOnlyList<TimeSpan> frameDurations,
        int playbackStartFrameCount)
    {
        ArgumentNullException.ThrowIfNull(frameDurations);

        if ((playbackStartFrameCount <= 0)
            || (playbackStartFrameCount
                > frameDurations.Count))
        {
            throw new ArgumentOutOfRangeException(
                nameof(playbackStartFrameCount),
                playbackStartFrameCount,
                $"The playback start frame count must be between 1 and {frameDurations.Count}.");
        }

        int fractionFrameCount = checked((int)Math.Ceiling(
            frameDurations.Count
            * _minimumInitialFrameFraction));
        int runwayFrameCount = GetRunwayFrameCount(
            frameDurations);

        return Math.Min(
            frameDurations.Count,
            Math.Max(
                playbackStartFrameCount,
                Math.Max(
                    fractionFrameCount,
                    runwayFrameCount)));
    }

    private int GetRunwayFrameCount(
        IReadOnlyList<TimeSpan> frameDurations)
    {
        TimeSpan accumulatedDuration = TimeSpan.Zero;

        for (int frameIndex = 0;
            frameIndex < frameDurations.Count;
            frameIndex++)
        {
            accumulatedDuration +=
                frameDurations[frameIndex];

            if (accumulatedDuration
                >= _minimumPlaybackRunway)
            {
                return frameIndex + 1;
            }
        }

        return frameDurations.Count;
    }
}
