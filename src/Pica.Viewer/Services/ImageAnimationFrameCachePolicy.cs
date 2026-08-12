using Avalonia;

namespace Pica.Viewer.Services;

internal sealed record ImageAnimationFrameCachePolicy
{
    internal static ImageAnimationFrameCachePolicy Default { get; } =
        new(DefaultMaximumCompleteAnimationBytes);

    internal long MaximumCompleteAnimationBytes { get; }

    private const int BytesPerPixel = 4;
    private const long DefaultMaximumCompleteAnimationBytes =
        256L * 1024L * 1024L;

    internal ImageAnimationFrameCachePolicy(
        long maximumCompleteAnimationBytes)
    {
        if (maximumCompleteAnimationBytes <= 0L)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumCompleteAnimationBytes),
                maximumCompleteAnimationBytes,
                "The complete animation cache size must be positive.");
        }

        MaximumCompleteAnimationBytes =
            maximumCompleteAnimationBytes;
    }

    internal static long GetFrameByteCount(
        PixelSize framePixelSize)
    {
        if ((framePixelSize.Width <= 0)
            || (framePixelSize.Height <= 0))
        {
            throw new ArgumentOutOfRangeException(
                nameof(framePixelSize),
                framePixelSize,
                "Animation frame dimensions must be positive.");
        }

        return checked(
            (long)framePixelSize.Width
            * framePixelSize.Height
            * BytesPerPixel);
    }

    internal bool CanRetainAnimation(
        int frameCount,
        PixelSize framePixelSize)
    {
        if (frameCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(frameCount),
                frameCount,
                "The animation frame count must be positive.");
        }

        long frameBytes = GetFrameByteCount(
            framePixelSize);

        return frameBytes
            <= MaximumCompleteAnimationBytes / frameCount;
    }
}
