using ImageMagick;

namespace Pica.Viewer.Services;

internal static class MagickAnimationMetadata
{
    private const double MillisecondsPerSecond = 1000d;
    private const double DefaultAnimationTicksPerSecond = 100d;

    internal static TimeSpan GetFrameDuration(
        IMagickImage<byte> image)
    {
        ArgumentNullException.ThrowIfNull(image);
        double ticksPerSecond =
            image.AnimationTicksPerSecond > 0
                ? image.AnimationTicksPerSecond
                : DefaultAnimationTicksPerSecond;
        double durationMilliseconds =
            image.AnimationDelay > 0
                ? image.AnimationDelay
                    * MillisecondsPerSecond
                    / ticksPerSecond
                : 0d;

        return ImageAnimationTiming.NormalizeFrameDuration(
            durationMilliseconds);
    }

    internal static ImageFramePresentationModes
        GetEffectivePresentationMode(
            IEnumerable<IMagickImage<byte>> images,
            ImageFramePresentationModes supportedMode)
    {
        ArgumentNullException.ThrowIfNull(images);
        bool supportsManualNavigation = supportedMode.HasFlag(
            ImageFramePresentationModes.ManualNavigation);
        bool supportsAutomaticPlayback = supportedMode.HasFlag(
            ImageFramePresentationModes.AutomaticPlayback);

        if (!supportsManualNavigation
            || !supportsAutomaticPlayback)
        {
            return supportedMode;
        }

        bool hasAnimationTiming = images.Any(
            image => image.AnimationDelay > 0);

        return hasAnimationTiming
            ? supportedMode
            : supportedMode
                & ~ImageFramePresentationModes.AutomaticPlayback;
    }
}
