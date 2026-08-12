using Avalonia;
using Avalonia.Headless;
using FluentAssertions;
using Xunit;

using Pica.Tests.Common;
using Pica.Viewer.Services;
using Pica.Viewer.Tests.TestDoubles;

namespace Pica.Viewer.Tests.Services;

[Collection(AvaloniaHeadlessCollection.Name)]
public sealed class ProgressiveImageDecoderTests
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
    public async Task Decode_WithFourFrames_DecodesSourceOnceAndPrimesReplayStart()
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(
            typeof(ProgressiveImageDecoderTests),
            SessionLock,
            async () =>
            {
                ControlledProgressiveImageFrameReader frameReader =
                    new(4);
                using DecodedImage image =
                    ProgressiveImageDecoder.Decode(
                        frameReader,
                        ImageFramePresentationModes.AutomaticPlayback,
                        ImageAnimationBufferingPolicy.Lightweight,
                        CancellationToken.None);
                using CancellationTokenSource timeout =
                    new(TestTimeout);
                await frameReader.RemainingFrameRequested.WaitAsync(
                    timeout.Token);

                image.FrameCount.Should().Be(4);
                image.LoadedFrameCount.Should().Be(2);
                image.IsPlaybackStartBufferReady.Should().BeTrue();
                image.DecodingCompletion.IsCompleted.Should().BeFalse();

                frameReader.ReleaseRemainingFrames();
                await WaitForFullDecodeAsync(
                    image,
                    timeout.Token);
                image.CancelDecoding();
                Exception? decodingException =
                    await image.DecodingCompletion.WaitAsync(
                        timeout.Token);

                decodingException.Should().BeNull();
                image.LoadedFrameCount.Should().Be(4);
                image.IsPlaybackStartBufferReady.Should().BeTrue();
                image.IsFullyDecoded.Should().BeTrue();
                frameReader.ReadFrameIndices
                    .Should()
                    .Equal(0, 1, 2, 3);
                frameReader.IsDisposed.Should().BeTrue();
            });
    }

    [Fact]
    public async Task Decode_WithShortWebpAnimation_WaitsForEveryFrameBeforePlaybackIsReady()
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(
            typeof(ProgressiveImageDecoderTests),
            SessionLock,
            async () =>
            {
                ControlledProgressiveImageFrameReader frameReader =
                    new(4);
                using DecodedImage image =
                    ProgressiveImageDecoder.Decode(
                        frameReader,
                        ImageFramePresentationModes.AutomaticPlayback,
                        ImageAnimationBufferingPolicy.WebP,
                        CancellationToken.None);
                using CancellationTokenSource timeout =
                    new(TestTimeout);
                await frameReader.RemainingFrameRequested.WaitAsync(
                    timeout.Token);

                image.PlaybackStartFrameCount.Should().Be(4);
                image.IsPlaybackStartBufferReady.Should().BeFalse();

                frameReader.ReleaseRemainingFrames();
                Exception? decodingException =
                    await image.DecodingCompletion.WaitAsync(
                        timeout.Token);

                decodingException.Should().BeNull();
                image.IsPlaybackStartBufferReady.Should().BeTrue();
                image.IsFullyDecoded.Should().BeTrue();
            });
    }

    [Fact]
    public async Task Decode_WithAnimationInsideCacheLimit_DecodesEveryFrameOnceAndRetainsIt()
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(
            typeof(ProgressiveImageDecoderTests),
            SessionLock,
            async () =>
            {
                const int frameCount = 100;
                ControlledProgressiveImageFrameReader frameReader =
                    new(frameCount);
                frameReader.ReleaseRemainingFrames();
                using DecodedImage image =
                    ProgressiveImageDecoder.Decode(
                        frameReader,
                        ImageFramePresentationModes.AutomaticPlayback,
                        ImageAnimationBufferingPolicy.WebP,
                        CancellationToken.None);
                using CancellationTokenSource timeout =
                    new(TestTimeout);
                await WaitForFullDecodeAsync(
                    image,
                    timeout.Token);
                frameReader.ReadFrameCount.Should().Be(
                    frameCount);

                for (int frameIndex = 0;
                    frameIndex < frameCount;
                    frameIndex++)
                {
                    image.SetPlaybackFrameIndex(frameIndex);
                    image.IsFrameAvailable(frameIndex)
                        .Should()
                        .BeTrue();
                }

                image.SetPlaybackFrameIndex(0);

                image.IsPlaybackStartBufferReady.Should().BeTrue();
                image.CancelDecoding();
                Exception? decodingException =
                    await image.DecodingCompletion.WaitAsync(
                        timeout.Token);
                decodingException.Should().BeNull();
                image.LoadedFrameCount.Should().Be(frameCount);
                image.IsFullyDecoded.Should().BeTrue();
                frameReader.ReadFrameCount.Should().Be(
                    frameCount);
                frameReader.IsDisposed.Should().BeTrue();
            });
    }

    [Fact]
    public async Task Decode_WithAnimationAboveCacheLimit_MaintainsBoundedPlaybackBuffer()
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(
            typeof(ProgressiveImageDecoderTests),
            SessionLock,
            async () =>
            {
                const int FrameCount = 100;
                ControlledProgressiveImageFrameReader frameReader =
                    new(FrameCount);
                frameReader.ReleaseRemainingFrames();
                using DecodedImage image =
                    ProgressiveImageDecoder.Decode(
                        frameReader,
                        ImageFramePresentationModes.AutomaticPlayback,
                        ImageAnimationBufferingPolicy.WebP,
                        CancellationToken.None,
                        new ImageAnimationFrameCachePolicy(32L));
                using CancellationTokenSource timeout =
                    new(TestTimeout);

                for (int frameIndex = 1;
                    frameIndex <= 25;
                    frameIndex++)
                {
                    image.SetPlaybackFrameIndex(frameIndex);
                    await WaitForPlaybackBufferAsync(
                        image,
                        timeout.Token);

                    image.LoadedFrameCount
                        .Should()
                        .BeLessThanOrEqualTo(
                            image.MaximumResidentFrameCount);
                }

                image.RetainsCompleteAnimation.Should().BeFalse();
                image.CancelDecoding();
                Exception? decodingException =
                    await image.DecodingCompletion.WaitAsync(
                        timeout.Token);

                decodingException.Should().BeNull();
                frameReader.IsDisposed.Should().BeTrue();
            });
    }

    private static async Task WaitForFullDecodeAsync(
        DecodedImage image,
        CancellationToken ct)
    {
        while (!image.IsFullyDecoded)
        {
            await Task.Delay(1, ct);
        }
    }

    private static async Task WaitForPlaybackBufferAsync(
        DecodedImage image,
        CancellationToken ct)
    {
        while (!image.IsPlaybackStartBufferReady)
        {
            await Task.Delay(1, ct);
        }
    }

}
