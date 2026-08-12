using Avalonia;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using FluentAssertions;
using ImageMagick;
using Xunit;

using Pica.Tests.Common;
using Pica.Viewer.Services;
using Pica.Viewer.Tests.TestDoubles;

namespace Pica.Viewer.Tests.Services;

[Collection(AvaloniaHeadlessCollection.Name)]
public sealed class MultiFrameImageDecoderTests
{
    private static readonly SemaphoreSlim SessionLock = new(1, 1);

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder
            .Configure<Application>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }

    [Fact]
    public async Task LoadAsync_WithAnimatedGif_DecodesFramesAndDurations()
    {
        await DispatchAsync(async () =>
        {
            using PicaTemporaryDirectory temporaryDirectory = new();
            string imagePath = Path.Combine(
                temporaryDirectory.DirectoryPath,
                "animated.gif");
            AnimatedImageTestData.Create(
                imagePath,
                MagickFormat.Gif);
            FullResolutionImageLoader loader = new(
                new ImageFormatRegistry(),
                MultiFrameImageDecoderTestFactory.Create());

            using DecodedImageContent content = await loader.LoadAsync(
                imagePath,
                CancellationToken.None);
            DecodedImage image = content.Groups[
                content.InitialGroupIndex].GetRequiredImage();

            image.Frames.Should().HaveCount(2);
            image.FramePresentationMode.Should().Be(
                ImageFramePresentationModes.AutomaticPlayback);
            image.Frames[0].Duration.Should().Be(
                AnimatedImageTestData.FirstFrameDuration);
            image.Frames[1].Duration.Should().Be(
                AnimatedImageTestData.SecondFrameDuration);
        });
    }

    [Fact]
    public async Task LoadAsync_WithFourFrameGif_RetainsEveryDecodedFrame()
    {
        await DispatchAsync(async () =>
        {
            using PicaTemporaryDirectory temporaryDirectory = new();
            string imagePath = Path.Combine(
                temporaryDirectory.DirectoryPath,
                "four-frames.gif");
            AnimatedImageTestData.CreateFourFrameGif(imagePath);
            FullResolutionImageLoader loader = new(
                new ImageFormatRegistry(),
                MultiFrameImageDecoderTestFactory.Create());

            using DecodedImageContent content = await loader.LoadAsync(
                imagePath,
                CancellationToken.None);
            DecodedImage image = content.Groups[
                content.InitialGroupIndex].GetRequiredImage();
            await WaitForFullDecodeAsync(image);

            for (int playbackFrame = 0;
                playbackFrame < 8;
                playbackFrame++)
            {
                int frameIndex = playbackFrame % image.FrameCount;
                image.SetPlaybackFrameIndex(frameIndex);
                await WaitForPlaybackBufferAsync(image);

                image.IsFrameAvailable(frameIndex).Should().BeTrue();
            }

            image.CancelDecoding();
            Exception? decodingException =
                await image.DecodingCompletion;

            decodingException.Should().BeNull();
            image.FrameCount.Should().Be(4);
            image.LoadedFrameCount.Should().Be(4);
            image.Frames.Should().HaveCount(4);
        });
    }

    [Theory]
    [InlineData("four-frames.webp", MagickFormat.WebP)]
    [InlineData("four-frames.avif", MagickFormat.Avif)]
    public async Task LoadAsync_WithFourFrameAnimation_LoadsPlaybackBuffer(
        string fileName,
        MagickFormat format)
    {
        await DispatchAsync(async () =>
        {
            using PicaTemporaryDirectory temporaryDirectory = new();
            string imagePath = Path.Combine(
                temporaryDirectory.DirectoryPath,
                fileName);
            AnimatedImageTestData.CreateFourFrameAnimation(
                imagePath,
                format);
            FullResolutionImageLoader loader = new(
                new ImageFormatRegistry(),
                MultiFrameImageDecoderTestFactory.Create());

            using DecodedImageContent content = await loader.LoadAsync(
                imagePath,
                CancellationToken.None);
            DecodedImage image = content.Groups[
                content.InitialGroupIndex].GetRequiredImage();
            await WaitForPlaybackBufferAsync(image);
            image.CancelDecoding();
            Exception? decodingException =
                await image.DecodingCompletion;

            decodingException.Should().BeNull();
            image.FrameCount.Should().Be(4);
            image.LoadedFrameCount.Should().Be(4);
            image.Frames.Should().HaveCount(4);
            image.FramePresentationMode.HasFlag(
                    ImageFramePresentationModes.AutomaticPlayback)
                .Should()
                .BeTrue();
        });
    }

    [Theory]
    [InlineData("long-animation.webp", MagickFormat.WebP)]
    [InlineData("long-animation.avif", MagickFormat.Avif)]
    public async Task LoadAsync_WithAnimationInsideCacheLimit_RetainsEveryDecodedFrame(
        string fileName,
        MagickFormat format)
    {
        await DispatchAsync(async () =>
        {
            const int FrameCount = 15;
            using PicaTemporaryDirectory temporaryDirectory = new();
            string imagePath = Path.Combine(
                temporaryDirectory.DirectoryPath,
                fileName);
            AnimatedImageTestData.CreateFrameAnimation(
                imagePath,
                format,
                FrameCount);
            FullResolutionImageLoader loader = new(
                new ImageFormatRegistry(),
                MultiFrameImageDecoderTestFactory.Create());

            using DecodedImageContent content = await loader.LoadAsync(
                imagePath,
                CancellationToken.None);
            DecodedImage image = content.Groups[
                content.InitialGroupIndex].GetRequiredImage();
            await WaitForFullDecodeAsync(image);
            image.CancelDecoding();
            Exception? decodingException =
                await image.DecodingCompletion;

            decodingException.Should().BeNull();
            image.FrameCount.Should().Be(FrameCount);
            image.LoadedFrameCount.Should().Be(FrameCount);
            image.Frames.Should().HaveCount(
                image.LoadedFrameCount);
            image.IsFullyDecoded.Should().BeTrue();
        });
    }

    [Theory]
    [InlineData("animated.webp", MagickFormat.WebP)]
    public async Task LoadAsync_WithAnimatedFormat_DecodesAutomaticFrames(
        string fileName,
        MagickFormat format)
    {
        await DispatchAsync(async () =>
        {
            using PicaTemporaryDirectory temporaryDirectory = new();
            string imagePath = Path.Combine(
                temporaryDirectory.DirectoryPath,
                fileName);
            AnimatedImageTestData.Create(imagePath, format);
            FullResolutionImageLoader loader = new(
                new ImageFormatRegistry(),
                MultiFrameImageDecoderTestFactory.Create());

            using DecodedImageContent content = await loader.LoadAsync(
                imagePath,
                CancellationToken.None);
            DecodedImage image = content.Groups[
                content.InitialGroupIndex].GetRequiredImage();

            image.Frames.Should().HaveCount(2);
            image.FramePresentationMode.Should().Be(
                ImageFramePresentationModes.AutomaticPlayback);
            image.Frames.Select(frame => frame.Duration)
                .Should()
                .Equal(
                    AnimatedImageTestData.FirstFrameDuration,
                    AnimatedImageTestData.SecondFrameDuration);
            image.Frames.Select(frame => frame.Bitmap.PixelSize)
                .Should()
                .OnlyContain(pixelSize =>
                    pixelSize == new PixelSize(4, 3));
            image.AnimationIterations.Should().Be(0);
        });
    }

    [Fact]
    public async Task LoadAsync_WithAnimatedAvif_DecodesBrowsableAnimation()
    {
        await DispatchAsync(async () =>
        {
            using PicaTemporaryDirectory temporaryDirectory = new();
            string imagePath = Path.Combine(
                temporaryDirectory.DirectoryPath,
                "animated.avif");
            AnimatedImageTestData.Create(
                imagePath,
                MagickFormat.Avif);
            TimeSpan[] expectedDurations =
            [
                AnimatedImageTestData.FirstFrameDuration,
                AnimatedImageTestData.SecondFrameDuration
            ];
            FullResolutionImageLoader loader = new(
                new ImageFormatRegistry(),
                MultiFrameImageDecoderTestFactory.Create());

            using DecodedImageContent content = await loader.LoadAsync(
                imagePath,
                CancellationToken.None);
            DecodedImage image = content.Groups[
                content.InitialGroupIndex].GetRequiredImage();

            image.Frames.Should().HaveCount(2);
            image.FramePresentationMode.Should().Be(
                ImageFramePresentationModes.ManualNavigation
                    | ImageFramePresentationModes.AutomaticPlayback);
            image.Frames.Select(frame => frame.Duration)
                .Should()
                .BeEquivalentTo(expectedDurations);
        });
    }

    [Theory]
    [InlineData("animated.png")]
    [InlineData("animated.apng")]
    public async Task LoadAsync_WithAnimatedPng_DecodesAutomaticFrames(
        string fileName)
    {
        await DispatchAsync(async () =>
        {
            using PicaTemporaryDirectory temporaryDirectory = new();
            string imagePath = Path.Combine(
                temporaryDirectory.DirectoryPath,
                fileName);
            ApngImageTestData.Create(imagePath);
            FullResolutionImageLoader loader = new(
                new ImageFormatRegistry(),
                MultiFrameImageDecoderTestFactory.Create());

            using DecodedImageContent content = await loader.LoadAsync(
                imagePath,
                CancellationToken.None);
            DecodedImage image = content.Groups[
                content.InitialGroupIndex].GetRequiredImage();

            image.Frames.Should().HaveCount(2);
            image.FramePresentationMode.Should().Be(
                ImageFramePresentationModes.AutomaticPlayback);
            image.Frames.Select(frame => frame.Duration)
                .Should()
                .Equal(
                    AnimatedImageTestData.FirstFrameDuration,
                    AnimatedImageTestData.SecondFrameDuration);
            image.Frames.Select(frame => frame.Bitmap.PixelSize)
                .Should()
                .OnlyContain(pixelSize =>
                    pixelSize == new PixelSize(4, 3));
            image.AnimationIterations.Should().Be(0);
        });
    }

    [Fact]
    public async Task Decode_WithUntimedImageCollection_DoesNotEnableAutomaticPlayback()
    {
        await DispatchAsync(() =>
        {
            using PicaTemporaryDirectory temporaryDirectory = new();
            string imagePath = Path.Combine(
                temporaryDirectory.DirectoryPath,
                "pages.tiff");
            TiffImageTestData.Create(imagePath);
            using FileStream stream = File.OpenRead(imagePath);
            IMultiFrameImageDecoder decoder =
                MultiFrameImageDecoderTestFactory.Create();

            using DecodedImage image = decoder.Decode(
                stream,
                new ImageDecoderSelection(
                    new MagickImageDecoder(),
                    ImageFramePresentationModes.ManualNavigation
                        | ImageFramePresentationModes.AutomaticPlayback),
                CancellationToken.None);

            image.FramePresentationMode.Should().Be(
                ImageFramePresentationModes.ManualNavigation);

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task Decode_WithSingleFrame_UsesProvidedDecoder()
    {
        await DispatchAsync(async () =>
        {
            using Bitmap sourceBitmap =
                BgraBitmapTestData.CreateBitmap();
            byte[] content = await new PngImageEncoder().EncodeAsync(
                sourceBitmap,
                CancellationToken.None);
            using MemoryStream stream = new(content);
            Bitmap expectedBitmap =
                BgraBitmapTestData.CreateBitmap();
            RecordingSingleFrameImageDecoder singleFrameDecoder =
                new(expectedBitmap);
            IMultiFrameImageDecoder decoder =
                MultiFrameImageDecoderTestFactory.Create();

            using DecodedImage image = decoder.Decode(
                stream,
                new ImageDecoderSelection(
                    singleFrameDecoder,
                    ImageFramePresentationModes.AutomaticPlayback,
                    ImageFrameDecoderKind.ApngAnimation),
                CancellationToken.None);

            image.Frames.Should().ContainSingle();
            image.Frames[0].Bitmap.Should().BeSameAs(expectedBitmap);
            singleFrameDecoder.DecodeCount.Should().Be(1);
        });
    }

    [Fact]
    public async Task LoadAsync_WithMultiImageIcon_DecodesBrowsableImages()
    {
        await DispatchAsync(async () =>
        {
            string imagePath = Path.Combine(
                AppContext.BaseDirectory,
                "AppIcon.ico");
            FullResolutionImageLoader loader = new(
                new ImageFormatRegistry(),
                MultiFrameImageDecoderTestFactory.Create());

            using DecodedImageContent content = await loader.LoadAsync(
                imagePath,
                CancellationToken.None);
            DecodedImage image = content.Groups[
                content.InitialGroupIndex].GetRequiredImage();

            image.Frames.Should().HaveCountGreaterThan(1);
            image.FramePresentationMode.Should().Be(
                ImageFramePresentationModes.ManualNavigation);
            image.FrameNumbering.Should().Be(
                ImageFrameNumbering.Reverse);
            image.Frames.Select(frame => frame.Bitmap.PixelSize.Width)
                .Should()
                .OnlyHaveUniqueItems();
            DecodedImageFrame initialFrame = image.GetFrame(
                image.PreferredInitialFrameIndex)
                ?? throw new InvalidOperationException(
                    "The icon initial frame must be available.");
            int largestWidth = image.Frames
                .Max(frame => frame.Bitmap.PixelSize.Width);
            initialFrame.Bitmap.PixelSize.Width.Should().Be(
                largestWidth);
        });
    }

    [Fact]
    public async Task LoadAsync_WithMultipleAnimationTracks_LoadsOnlySelectedTrack()
    {
        await DispatchAsync(async () =>
        {
            using PicaTemporaryDirectory temporaryDirectory = new();
            string imagePath = Path.Combine(
                temporaryDirectory.DirectoryPath,
                "multiple-animations.avif");
            byte[] source = IsoBmffAnimationTestData
                .CreateTimedAv1Sequences(2);
            await File.WriteAllBytesAsync(
                imagePath,
                source);
            RecordingMultiFrameImageDecoder decoder = new(
                () => DecodedImage.CreateSingle(
                    BgraBitmapTestData.CreateBitmap()));
            FullResolutionImageLoader loader = new(
                new ImageFormatRegistry(),
                decoder);

            using DecodedImageContent content = await loader.LoadAsync(
                imagePath,
                CancellationToken.None);
            DecodedImage secondAnimation = await content.Groups[1]
                .LoadAsync(CancellationToken.None);

            content.Groups.Should().HaveCount(2);
            content.Groups.Select(group =>
                    group.Definition.Kind)
                .Should()
                .OnlyContain(kind =>
                    kind == ImageContentGroupKind.Animation);
            content.Groups.Select(group =>
                    group.Definition.ItemCount)
                .Should()
                .Equal(3, 3);
            decoder.SelectedTrackIndices.Should().Equal(0, 1);
            secondAnimation.Frames.Should().ContainSingle();
        });
    }

    [Fact]
    public async Task LoadAsync_WithDuplicatedAvifAnimation_DecodesBothTracks()
    {
        await DispatchAsync(async () =>
        {
            const int FrameCount = 4;
            using PicaTemporaryDirectory temporaryDirectory = new();
            string sourcePath = Path.Combine(
                temporaryDirectory.DirectoryPath,
                "source.avif");
            string multipleAnimationPath = Path.Combine(
                temporaryDirectory.DirectoryPath,
                "multiple-animations.avif");
            AnimatedImageTestData.CreateFrameAnimation(
                sourcePath,
                MagickFormat.Avif,
                FrameCount);
            byte[] source = await File.ReadAllBytesAsync(
                sourcePath);
            byte[] multipleAnimation =
                IsoBmffMultiAnimationTestData
                    .DuplicateAnimationTracks(source);
            await File.WriteAllBytesAsync(
                multipleAnimationPath,
                multipleAnimation);
            FullResolutionImageLoader loader = new(
                new ImageFormatRegistry(),
                MultiFrameImageDecoderTestFactory.Create());

            using DecodedImageContent content = await loader.LoadAsync(
                multipleAnimationPath,
                CancellationToken.None);
            DecodedImage firstAnimation = content.Groups[0]
                .GetRequiredImage();
            DecodedImage secondAnimation = await content.Groups[1]
                .LoadAsync(CancellationToken.None);
            await WaitForFullDecodeAsync(firstAnimation);
            await WaitForFullDecodeAsync(secondAnimation);
            firstAnimation.CancelDecoding();
            secondAnimation.CancelDecoding();
            Exception? firstException =
                await firstAnimation.DecodingCompletion;
            Exception? secondException =
                await secondAnimation.DecodingCompletion;

            content.Groups.Should().HaveCount(2);
            firstException.Should().BeNull();
            secondException.Should().BeNull();
            firstAnimation.FrameCount.Should().Be(FrameCount);
            secondAnimation.FrameCount.Should().Be(FrameCount);
            firstAnimation.LoadedFrameCount.Should().Be(FrameCount);
            secondAnimation.LoadedFrameCount.Should().Be(FrameCount);
        });
    }

    [Fact]
    public async Task LoadAsync_WhileDecoderIsRunning_ReleasesSourceFile()
    {
        await DispatchAsync(async () =>
        {
            using PicaTemporaryDirectory temporaryDirectory = new();
            string imagePath = Path.Combine(
                temporaryDirectory.DirectoryPath,
                "source.png");
            await File.WriteAllBytesAsync(
                imagePath,
                new byte[] { 1, 2, 3, 4 });
            DecodedImage image = DecodedImage.CreateSingle(
                BgraBitmapTestData.CreateBitmap());
            using BlockingMultiFrameImageDecoder decoder = new(image);
            FullResolutionImageLoader loader = new(
                new ImageFormatRegistry(),
                decoder);
            Task<DecodedImageContent> loadingTask = loader.LoadAsync(
                imagePath,
                CancellationToken.None);
            await decoder.OperationStarted;

            try
            {
                using FileStream exclusiveStream = new(
                    imagePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.None);

                exclusiveStream.CanRead.Should().BeTrue();
            }
            finally
            {
                decoder.Release();
            }

            using DecodedImageContent content = await loadingTask;
            content.Groups.Should().ContainSingle();
        });
    }

    private static async Task DispatchAsync(Func<Task> action)
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(
            typeof(MultiFrameImageDecoderTests),
            SessionLock,
            action);
    }

    private static async Task WaitForPlaybackBufferAsync(
        DecodedImage image)
    {
        using CancellationTokenSource timeout =
            new(TimeSpan.FromSeconds(10d));

        while (!image.IsPlaybackStartBufferReady)
        {
            await Task.Delay(1, timeout.Token);
        }
    }

    private static async Task WaitForFullDecodeAsync(
        DecodedImage image)
    {
        using CancellationTokenSource timeout =
            new(TimeSpan.FromSeconds(10d));

        while (!image.IsFullyDecoded)
        {
            await Task.Delay(1, timeout.Token);
        }
    }
}
