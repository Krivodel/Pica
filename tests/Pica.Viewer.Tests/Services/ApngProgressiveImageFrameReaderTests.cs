using Avalonia;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using FluentAssertions;
using Xunit;

using Pica.Tests.Common;
using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.Services;

[Collection(AvaloniaHeadlessCollection.Name)]
public sealed class ApngProgressiveImageFrameReaderTests
{
    private static readonly SemaphoreSlim SessionLock = new(1, 1);

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder
            .Configure<Application>()
            .UseHeadless(
                new AvaloniaHeadlessPlatformOptions());
    }

    [Fact]
    public async Task ReadFrame_WithTransparentSourcePixel_ClearsPreviousCanvasPixel()
    {
        await DispatchAsync(() =>
        {
            using MemoryStream stream = new(
                ApngImageTestData.GetSourceClearContent());
            ApngAnimationData animation =
                ApngParser.Parse(
                    stream,
                    CancellationToken.None);
            using ApngProgressiveImageFrameReader reader =
                new(animation);
            DecodedImageFrame firstFrame =
                reader.ReadFrame(
                    0,
                    CancellationToken.None);
            using Bitmap firstBitmap =
                firstFrame.Bitmap;
            DecodedImageFrame secondFrame =
                reader.ReadFrame(
                    1,
                    CancellationToken.None);
            using Bitmap secondBitmap =
                secondFrame.Bitmap;
            PreparedBitmapPixels pixels =
                BitmapPixelReader.Read(
                    secondBitmap,
                    CancellationToken.None);

            pixels.BgraPixels.Should().Equal(
                0, 0, 0, 0,
                0, 255, 0, 255);
        });
    }

    [Fact]
    public async Task ReadFrame_AfterSequenceWrap_ReconstructsFirstFrame()
    {
        await DispatchAsync(() =>
        {
            using MemoryStream stream = new(
                ApngImageTestData.GetSourceClearContent());
            ApngAnimationData animation =
                ApngParser.Parse(
                    stream,
                    CancellationToken.None);
            using ApngProgressiveImageFrameReader reader =
                new(animation);
            using Bitmap firstBitmap = reader.ReadFrame(
                    0,
                    CancellationToken.None)
                .Bitmap;
            byte[] firstPixels = BitmapPixelReader.Read(
                    firstBitmap,
                    CancellationToken.None)
                .BgraPixels;
            using Bitmap secondBitmap = reader.ReadFrame(
                    1,
                    CancellationToken.None)
                .Bitmap;
            using Bitmap repeatedFirstBitmap = reader.ReadFrame(
                    0,
                    CancellationToken.None)
                .Bitmap;
            byte[] repeatedFirstPixels = BitmapPixelReader.Read(
                    repeatedFirstBitmap,
                    CancellationToken.None)
                .BgraPixels;

            repeatedFirstPixels.Should().Equal(firstPixels);
        });
    }

    [Fact]
    public void Parse_WithPartialSourceFrame_ReadsFrameControlData()
    {
        using MemoryStream stream = new(
            ApngImageTestData.GetSourceClearContent());

        ApngAnimationData animation =
            ApngParser.Parse(
                stream,
                CancellationToken.None);

        animation.Frames.Should().HaveCount(2);
        animation.Frames[1].Should().Match<ApngFrameData>(
            frame =>
                (frame.X == 0)
                && (frame.Y == 0)
                && (frame.Width == 1)
                && (frame.Height == 1)
                && (frame.DisposeOperation
                    == ApngDisposeOperation.None)
                && (frame.BlendOperation
                    == ApngBlendOperation.Source));
    }

    [Fact]
    public void Parse_WithSeparateDefaultImage_ExcludesDefaultFromAnimation()
    {
        using MemoryStream stream = new(
            ApngImageTestData.GetSeparateDefaultContent());

        ApngAnimationData animation =
            ApngParser.Parse(
                stream,
                CancellationToken.None);

        animation.Frames.Should().HaveCount(2);
        animation.AnimationIterations.Should().Be(3);
        animation.Frames
            .Select(frame => frame.Duration)
            .Should()
            .Equal(
                TimeSpan.FromMilliseconds(50d),
                TimeSpan.FromMilliseconds(120d));
    }

    [Fact]
    public async Task ReadFrame_WithSeparateDefaultImage_StartsWithFirstAnimationFrame()
    {
        await DispatchAsync(() =>
        {
            using MemoryStream stream = new(
                ApngImageTestData.GetSeparateDefaultContent());
            ApngAnimationData animation =
                ApngParser.Parse(
                    stream,
                    CancellationToken.None);
            using ApngProgressiveImageFrameReader reader =
                new(animation);
            DecodedImageFrame frame =
                reader.ReadFrame(
                    0,
                    CancellationToken.None);
            using Bitmap bitmap = frame.Bitmap;
            PreparedBitmapPixels pixels =
                BitmapPixelReader.Read(
                    bitmap,
                    CancellationToken.None);

            pixels.BgraPixels.Should().Equal(
                0, 0, 255, 255,
                0, 0, 255, 255);
        });
    }

    private static async Task DispatchAsync(
        Action action)
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(
            typeof(ApngProgressiveImageFrameReaderTests),
            SessionLock,
            action);
    }
}
