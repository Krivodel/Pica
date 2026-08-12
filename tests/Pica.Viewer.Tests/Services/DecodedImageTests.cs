using Avalonia;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using FluentAssertions;
using Xunit;

using Pica.Tests.Common;
using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.Services;

[Collection(AvaloniaHeadlessCollection.Name)]
public sealed class DecodedImageTests
{
    private static readonly TimeSpan TestTimeout =
        TimeSpan.FromSeconds(5d);
    private static readonly SemaphoreSlim SessionLock =
        new(1, 1);

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder
            .Configure<Application>()
            .UseHeadless(
                new AvaloniaHeadlessPlatformOptions());
    }

    [Fact]
    public async Task StartDecoding_WithRemainingFrame_ReturnsBeforeFrameIsDecoded()
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(
            typeof(DecodedImageTests),
            SessionLock,
            async () =>
            {
                Bitmap firstBitmap =
                    BgraBitmapTestData.CreateBitmap();
                Bitmap secondBitmap =
                    BgraBitmapTestData.CreateBitmap();
                Bitmap thirdBitmap =
                    BgraBitmapTestData.CreateBitmap();
                using ManualResetEventSlim releaseDecoding =
                    new(false);
                TaskCompletionSource decodingStarted = new(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                using DecodedImage image =
                    DecodedImage.CreateProgressive(
                        3,
                        ImageFramePresentationModes.AutomaticPlayback,
                        0,
                        2);
                image.AddFrame(
                    0,
                    new DecodedImageFrame(
                        firstBitmap,
                        TimeSpan.Zero));
                image.AddFrame(
                    1,
                    new DecodedImageFrame(
                        secondBitmap,
                        TimeSpan.Zero));

                image.StartDecoding(
                    ct =>
                    {
                        decodingStarted.TrySetResult();
                        releaseDecoding.Wait(ct);
                        image.AddFrame(
                            2,
                            new DecodedImageFrame(
                                thirdBitmap,
                                TimeSpan.Zero));
                    });
                using CancellationTokenSource timeout =
                    new(TestTimeout);
                await decodingStarted.Task.WaitAsync(
                    timeout.Token);

                image.FrameCount.Should().Be(3);
                image.LoadedFrameCount.Should().Be(2);
                image.DecodingCompletion.IsCompleted
                    .Should()
                    .BeFalse();

                releaseDecoding.Set();
                Exception? decodingException =
                    await image.DecodingCompletion.WaitAsync(
                        timeout.Token);

                decodingException.Should().BeNull();
                image.LoadedFrameCount.Should().Be(3);
            });
    }

    [Fact]
    public async Task Dispose_WithStoredFrames_UsesReleaseHandler()
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(
            typeof(DecodedImageTests),
            SessionLock,
            () =>
            {
                List<Bitmap> releasedBitmaps = [];
                DecodedImage image =
                    DecodedImage.CreateProgressive(
                        3,
                        ImageFramePresentationModes.AutomaticPlayback,
                        0,
                        2);
                Bitmap firstBitmap =
                    BgraBitmapTestData.CreateBitmap();
                Bitmap secondBitmap =
                    BgraBitmapTestData.CreateBitmap();
                image.AddFrame(
                    0,
                    new DecodedImageFrame(
                        firstBitmap,
                        TimeSpan.Zero));
                image.AddFrame(
                    1,
                    new DecodedImageFrame(
                        secondBitmap,
                        TimeSpan.Zero));
                image.SetStoredBitmapReleaseHandler(
                    bitmap =>
                    {
                        releasedBitmaps.Add(bitmap);
                        bitmap.Dispose();
                    });

                image.Dispose();

                releasedBitmaps.Should().Equal(
                    firstBitmap,
                    secondBitmap);

                return Task.CompletedTask;
            });
    }

    [Fact]
    public async Task SetPlaybackFrameIndex_AfterPlaybackAdvances_RetainsDecodedFrames()
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(
            typeof(DecodedImageTests),
            SessionLock,
            () =>
            {
                using DecodedImage image =
                    DecodedImage.CreateProgressive(
                        5,
                        ImageFramePresentationModes.AutomaticPlayback,
                        0,
                        2);
                image.AddFrame(
                    0,
                    new DecodedImageFrame(
                        BgraBitmapTestData.CreateBitmap(),
                        TimeSpan.Zero));
                image.AddFrame(
                    1,
                    new DecodedImageFrame(
                        BgraBitmapTestData.CreateBitmap(),
                        TimeSpan.Zero));

                image.SetPlaybackFrameIndex(2);

                image.IsFrameAvailable(0).Should().BeTrue();
                image.IsFrameAvailable(1).Should().BeTrue();

                return Task.CompletedTask;
            });
    }

    [Fact]
    public async Task SetPlaybackFrameIndex_AfterPlaybackAdvances_RetainsCompleteAnimation()
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(
            typeof(DecodedImageTests),
            SessionLock,
            () =>
            {
                using DecodedImage image =
                    DecodedImage.CreateProgressive(
                        20,
                        ImageFramePresentationModes.AutomaticPlayback,
                        0,
                        8);
                image.SetPlaybackFrameIndex(10);

                for (int frameIndex = 0;
                    frameIndex < image.FrameCount;
                    frameIndex++)
                {
                    image.AddFrame(
                        frameIndex,
                        new DecodedImageFrame(
                            BgraBitmapTestData.CreateBitmap(),
                            TimeSpan.Zero));
                }

                image.LoadedFrameCount.Should().Be(20);
                Enumerable.Range(0, 20)
                    .Should()
                    .OnlyContain(frameIndex =>
                        image.IsFrameAvailable(frameIndex));

                return Task.CompletedTask;
            });
    }

    [Fact]
    public async Task SetPlaybackFrameIndex_WhenAnimationExceedsCacheLimit_KeepsBoundedFrameBuffer()
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(
            typeof(DecodedImageTests),
            SessionLock,
            () =>
            {
                ImageAnimationFrameCachePolicy frameCachePolicy =
                    new(32L);
                using DecodedImage image =
                    DecodedImage.CreateProgressive(
                        10,
                        ImageFramePresentationModes.AutomaticPlayback,
                        0,
                        2,
                        frameCachePolicy);

                for (int frameIndex = 0;
                    frameIndex < image.FrameCount;
                    frameIndex++)
                {
                    image.AddFrame(
                        frameIndex,
                        new DecodedImageFrame(
                            BgraBitmapTestData.CreateBitmap(),
                            TimeSpan.Zero));
                }

                image.SetPlaybackFrameIndex(2);
                image.AddFrame(
                    2,
                    new DecodedImageFrame(
                        BgraBitmapTestData.CreateBitmap(),
                        TimeSpan.Zero));
                image.AddFrame(
                    3,
                    new DecodedImageFrame(
                        BgraBitmapTestData.CreateBitmap(),
                        TimeSpan.Zero));

                image.RetainsCompleteAnimation.Should().BeFalse();
                image.LoadedFrameCount.Should().BeLessThanOrEqualTo(
                    image.MaximumResidentFrameCount);
                image.IsFrameAvailable(0).Should().BeTrue();
                image.IsFrameAvailable(1).Should().BeTrue();
                image.IsFrameAvailable(2).Should().BeTrue();
                image.IsFrameAvailable(3).Should().BeTrue();

                return Task.CompletedTask;
            });
    }
}
