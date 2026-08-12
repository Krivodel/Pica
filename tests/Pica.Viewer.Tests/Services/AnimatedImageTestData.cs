using ImageMagick;

namespace Pica.Viewer.Tests.Services;

internal static class AnimatedImageTestData
{
    internal static readonly TimeSpan FirstFrameDuration =
        TimeSpan.FromMilliseconds(50d);
    internal static readonly TimeSpan SecondFrameDuration =
        TimeSpan.FromMilliseconds(120d);

    private const int AnimationTicksPerSecond = 100;
    private const int FrameColorCount = 4;

    internal static void Create(
        string path,
        MagickFormat format)
    {
        using MagickImageCollection images = new();
        images.Add(new MagickImage(MagickColors.Red, 4, 3));
        images.Add(new MagickImage(MagickColors.Blue, 4, 3));
        images[0].AnimationTicksPerSecond =
            AnimationTicksPerSecond;
        images[0].AnimationDelay = 5;
        images[1].AnimationTicksPerSecond =
            AnimationTicksPerSecond;
        images[1].AnimationDelay = 12;
        images[0].Format = format;
        images[1].Format = format;
        images.Write(path, format);
    }

    internal static void CreateFourFrameGif(string path)
    {
        CreateFourFrameAnimation(
            path,
            MagickFormat.Gif);
    }

    internal static void CreateFourFrameAnimation(
        string path,
        MagickFormat format)
    {
        CreateFrameAnimation(
            path,
            format,
            4);
    }

    internal static void CreateFrameAnimation(
        string path,
        MagickFormat format,
        int frameCount)
    {
        if (frameCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(frameCount),
                frameCount,
                "The animation frame count must be positive.");
        }

        using MagickImageCollection images = new();

        for (int i = 0; i < frameCount; i++)
        {
            IMagickColor<byte> color =
                GetFrameColor(i);
            images.Add(
                new MagickImage(
                    color,
                    4,
                    3));
            images[i].AnimationTicksPerSecond =
                AnimationTicksPerSecond;
            images[i].AnimationDelay = 5;
            images[i].Format = format;
        }

        images.Write(path, format);
    }

    private static IMagickColor<byte> GetFrameColor(
        int frameIndex)
    {
        return (frameIndex % FrameColorCount) switch
        {
            0 => MagickColors.Red,
            1 => MagickColors.Blue,
            2 => MagickColors.Green,
            _ => MagickColors.Yellow
        };
    }
}
