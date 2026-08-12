namespace Pica.Viewer.Services;

internal sealed class ImageAnimationTimeline
{
    internal static ImageAnimationTimeline Empty { get; } =
        new ImageAnimationTimeline(Array.Empty<TimeSpan>());

    internal IReadOnlyList<TimeSpan> FrameDurations =>
        _frameDurations;
    internal TimeSpan Duration =>
        TimeSpan.FromTicks(_frameStartTicks[^1]);

    private readonly IReadOnlyList<TimeSpan> _frameDurations;
    private readonly long[] _frameStartTicks;

    internal ImageAnimationTimeline(
        IReadOnlyList<TimeSpan> frameDurations)
    {
        ArgumentNullException.ThrowIfNull(frameDurations);
        TimeSpan[] durations = new TimeSpan[frameDurations.Count];
        _frameStartTicks = new long[frameDurations.Count + 1];
        long elapsedTicks = 0L;

        for (int frameIndex = 0;
            frameIndex < frameDurations.Count;
            frameIndex++)
        {
            TimeSpan duration = frameDurations[frameIndex];

            if (duration < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(frameDurations),
                    duration,
                    "Animation frame durations must not be negative.");
            }

            durations[frameIndex] = duration;
            elapsedTicks = checked(elapsedTicks + duration.Ticks);
            _frameStartTicks[frameIndex + 1] = elapsedTicks;
        }

        _frameDurations = Array.AsReadOnly(durations);
    }

    internal TimeSpan GetFrameStartPosition(int frameIndex)
    {
        ValidateFrameIndex(frameIndex);

        return TimeSpan.FromTicks(
            _frameStartTicks[frameIndex]);
    }

    private void ValidateFrameIndex(int frameIndex)
    {
        if ((frameIndex < 0)
            || (frameIndex >= _frameDurations.Count))
        {
            throw new ArgumentOutOfRangeException(
                nameof(frameIndex),
                frameIndex,
                $"The frame index must be between 0 and {_frameDurations.Count - 1}.");
        }
    }
}
