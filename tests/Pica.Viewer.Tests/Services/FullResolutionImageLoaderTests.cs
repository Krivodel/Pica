using Avalonia;
using FluentAssertions;
using ImageMagick;
using Xunit;

using Pica.Tests.Common;
using Pica.Viewer.Services;
using Pica.Viewer.Tests.TestDoubles;

namespace Pica.Viewer.Tests.Services;

[Collection(AvaloniaHeadlessCollection.Name)]
public sealed class FullResolutionImageLoaderTests
{
    private static readonly SemaphoreSlim SessionLock = new(1, 1);

    [Theory]
    [InlineData(24, false, 0x00)]
    [InlineData(24, false, 0x30)]
    [InlineData(24, true, 0x10)]
    [InlineData(24, true, 0x20)]
    [InlineData(32, false, 0x10)]
    [InlineData(32, false, 0x20)]
    [InlineData(32, true, 0x00)]
    [InlineData(32, true, 0x30)]
    public async Task LoadAsync_WithTgaVariants_PreservesDimensionsOrientationAndAlpha(
        int pixelDepth,
        bool compressed,
        byte origin)
    {
        await DispatchAsync(async () =>
        {
            byte[] data = TgaImageTestData.Create(pixelDepth, compressed, origin);
            FullResolutionImageLoader loader = CreateLoader();

            using DecodedImageContent content = await loader.LoadAsync(
                data, "image" + PicaImageFormats.TgaExtension, CancellationToken.None);
            DecodedImage image = content.Groups[content.InitialGroupIndex].GetRequiredImage();
            PreparedBitmapPixels pixels = BitmapPixelReader.Read(image.Frames[0].Bitmap, CancellationToken.None);

            image.Frames.Should().ContainSingle();
            image.FramePresentationMode.Should().Be(ImageFramePresentationModes.None);
            image.Frames[0].Bitmap.PixelSize.Should().Be(new PixelSize(TgaImageTestData.Width, TgaImageTestData.Height));
            pixels.BgraPixels.Should().Equal(TgaImageTestData.GetExpectedPixels(pixelDepth));
        });
    }

    [Fact]
    public async Task LoadAsync_WithTgaFile_DecodesAndReleasesSourceFile()
    {
        await DispatchAsync(async () =>
        {
            using PicaTemporaryDirectory directory = new();
            string path = Path.Combine(directory.DirectoryPath, "image" + PicaImageFormats.TgaExtension);
            await File.WriteAllBytesAsync(path, TgaImageTestData.Create(32, true, 0x20));
            FullResolutionImageLoader loader = CreateLoader();

            using DecodedImageContent content = await loader.LoadAsync(path, CancellationToken.None);
            File.Delete(path);
            DecodedImage image = content.Groups[content.InitialGroupIndex].GetRequiredImage();

            image.Frames.Should().ContainSingle();
            BitmapPixelReader.Read(image.Frames[0].Bitmap, CancellationToken.None).BgraPixels
                .Should().Equal(TgaImageTestData.GetExpectedPixels(32));
            File.Exists(path).Should().BeFalse();
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LoadAsync_WithEightBitTga_DecodesGrayscaleOrPalette(bool colorMapped)
    {
        await DispatchAsync(async () =>
        {
            (byte[] data, byte[] expectedPixels) = TgaImageTestData.CreateEightBit(colorMapped);
            FullResolutionImageLoader loader = CreateLoader();

            using DecodedImageContent content = await loader.LoadAsync(
                data, "image" + PicaImageFormats.TgaExtension, CancellationToken.None);
            DecodedImage image = content.Groups[content.InitialGroupIndex].GetRequiredImage();

            image.Frames.Should().ContainSingle();
            BitmapPixelReader.Read(image.Frames[0].Bitmap, CancellationToken.None).BgraPixels
                .Should().Equal(expectedPixels);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LoadAsync_WithTruncatedTga_RejectsInvalidPixels(bool compressed)
    {
        await DispatchAsync(async () =>
        {
            byte[] data = TgaImageTestData.Create(32, compressed, 0x20);
            FullResolutionImageLoader loader = CreateLoader();
            Func<Task> load = async () =>
            {
                using DecodedImageContent content = await loader.LoadAsync(
                    data[..TgaImageTestData.HeaderLength], "image" + PicaImageFormats.TgaExtension, CancellationToken.None);
            };

            await load.Should().ThrowAsync<MagickException>();
        });
    }

    private static FullResolutionImageLoader CreateLoader()
    {
        return new FullResolutionImageLoader(new ImageFormatRegistry(), MultiFrameImageDecoderTestFactory.Create());
    }

    private static Task DispatchAsync(Func<Task> action)
    {
        return HeadlessTestSessionDispatcher.DispatchAsync(typeof(SkiaViewerTestSession), SessionLock, action);
    }
}
