namespace Pica.Viewer.Services;

internal sealed record ImageAnimationBufferingPolicy
{
    internal static ImageAnimationBufferingPolicy Lightweight { get; } =
        new(2);
    internal static ImageAnimationBufferingPolicy WebP { get; } =
        new(8);
    internal static ImageAnimationBufferingPolicy HighEfficiency { get; } =
        new(12);

    internal int InitialSynchronousFrameCount { get; }
    internal int PlaybackStartFrameCount { get; }

    private const int DefaultInitialSynchronousFrameCount =
        2;

    private ImageAnimationBufferingPolicy(
        int playbackStartFrameCount)
    {
        if (playbackStartFrameCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(playbackStartFrameCount),
                playbackStartFrameCount,
                "The playback start frame count must be positive.");
        }

        InitialSynchronousFrameCount =
            DefaultInitialSynchronousFrameCount;
        PlaybackStartFrameCount =
            playbackStartFrameCount;
    }

    internal int GetInitialSynchronousFrameCount(
        int frameCount)
    {
        ValidateFrameCount(frameCount);

        return Math.Min(
            InitialSynchronousFrameCount,
            frameCount);
    }

    internal int GetRequiredFrameCount(int frameCount)
    {
        ValidateFrameCount(frameCount);

        return Math.Min(
            PlaybackStartFrameCount,
            frameCount);
    }

    private static void ValidateFrameCount(
        int frameCount)
    {
        if (frameCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(frameCount),
                frameCount,
                "The animation frame count must be positive.");
        }
    }
}
