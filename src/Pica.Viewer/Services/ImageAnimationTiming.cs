namespace Pica.Viewer.Services;

internal static class ImageAnimationTiming
{
    private const double DefaultFrameDurationMilliseconds = 100d;
    private const double MinimumFrameDurationMilliseconds = 10d;

    internal static TimeSpan NormalizeFrameDuration(
        double durationMilliseconds)
    {
        double effectiveDurationMilliseconds =
            durationMilliseconds > 0d
                ? durationMilliseconds
                : DefaultFrameDurationMilliseconds;

        return TimeSpan.FromMilliseconds(Math.Max(
            MinimumFrameDurationMilliseconds,
            effectiveDurationMilliseconds));
    }
}
