using FluentAssertions;
using ImageMagick;
using Xunit;

using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.Services;

public sealed class ImageFormatRegistryTests
{
    private readonly ImageFormatRegistry _registry = new();

    [Theory]
    [InlineData("image.png")]
    [InlineData("image.apng")]
    [InlineData("image.jpg")]
    [InlineData("image.jpeg")]
    [InlineData("image.webp")]
    [InlineData("image.bmp")]
    [InlineData("image.gif")]
    [InlineData("image.ico")]
    [InlineData("image.cur")]
    [InlineData("image.avif")]
    [InlineData("image.heic")]
    [InlineData("image.heif")]
    [InlineData("image.tif")]
    [InlineData("image.tiff")]
    [InlineData("IMAGE.AVIF")]
    [InlineData("IMAGE.HEIC")]
    [InlineData("IMAGE.HEIF")]
    [InlineData("IMAGE.TIFF")]
    public void IsSupportedFileName_WithSupportedExtension_ReturnsTrue(string fileName)
    {
        bool isSupported = _registry.IsSupportedFileName(fileName);

        isSupported.Should().BeTrue();
    }

    [Theory]
    [InlineData("image.avif", PicaImageFormats.AvifContentType)]
    [InlineData("image.heic", PicaImageFormats.HeicContentType)]
    [InlineData("image.heif", PicaImageFormats.HeifContentType)]
    [InlineData("image.tif", PicaImageFormats.TiffContentType)]
    [InlineData("image.tiff", PicaImageFormats.TiffContentType)]
    public void GetContentType_WithMagickExtension_ReturnsExpectedContentType(
        string fileName,
        string expectedContentType)
    {
        string contentType = _registry.GetContentType(fileName);

        contentType.Should().Be(expectedContentType);
    }

    [Theory]
    [InlineData("image.png", typeof(AvaloniaBitmapDecoder))]
    [InlineData("image.jpg", typeof(AvaloniaBitmapDecoder))]
    [InlineData("image.jpeg", typeof(AvaloniaBitmapDecoder))]
    [InlineData("image.webp", typeof(AvaloniaBitmapDecoder))]
    [InlineData("image.bmp", typeof(AvaloniaBitmapDecoder))]
    [InlineData("image.gif", typeof(AvaloniaBitmapDecoder))]
    [InlineData("image.ico", typeof(IcoImageDecoder))]
    [InlineData("image.avif", typeof(MagickImageDecoder))]
    [InlineData("image.heic", typeof(MagickImageDecoder))]
    [InlineData("image.heif", typeof(MagickImageDecoder))]
    [InlineData("image.tif", typeof(MagickImageDecoder))]
    [InlineData("image.tiff", typeof(MagickImageDecoder))]
    public void Resolve_WithSupportedExtension_ReturnsExpectedDecoder(
        string fileName,
        Type expectedDecoderType)
    {
        ImageDecoderSelection decoderSelection =
            ((IImageDecoderResolver)_registry).Resolve(fileName);

        decoderSelection.Decoder.GetType().Should().Be(expectedDecoderType);
    }

    [Theory]
    [InlineData(
        "image.gif",
        (int)ImageFramePresentationModes.AutomaticPlayback)]
    [InlineData(
        "image.webp",
        (int)ImageFramePresentationModes.AutomaticPlayback)]
    [InlineData(
        "image.png",
        (int)ImageFramePresentationModes.AutomaticPlayback)]
    [InlineData(
        "image.ico",
        (int)ImageFramePresentationModes.ManualNavigation)]
    [InlineData(
        "image.cur",
        (int)ImageFramePresentationModes.ManualNavigation)]
    [InlineData(
        "image.tiff",
        (int)ImageFramePresentationModes.ManualNavigation)]
    [InlineData(
        "image.avif",
        (int)(ImageFramePresentationModes.ManualNavigation
            | ImageFramePresentationModes.AutomaticPlayback))]
    public void Resolve_WithMultiFrameExtension_ReturnsExpectedPresentationMode(
        string fileName,
        int expectedMode)
    {
        ImageDecoderSelection decoderSelection =
            ((IImageDecoderResolver)_registry).Resolve(fileName);

        decoderSelection.FramePresentationMode.Should().Be(
            (ImageFramePresentationModes)expectedMode);
    }

    [Theory]
    [InlineData("image.ico")]
    [InlineData("image.cur")]
    public void Resolve_WithIconExtension_ReturnsIconReadFormat(
        string fileName)
    {
        ImageDecoderSelection decoderSelection =
            ((IImageDecoderResolver)_registry).Resolve(fileName);

        decoderSelection.MultiFrameReadFormat.Should().Be(
            MagickFormat.Ico);
    }

    [Theory]
    [InlineData("image.png")]
    [InlineData("image.apng")]
    public void Resolve_WithPngExtension_ReturnsApngFrameDecoder(
        string fileName)
    {
        ImageDecoderSelection decoderSelection =
            ((IImageDecoderResolver)_registry).Resolve(fileName);

        decoderSelection.FrameDecoderKind.Should().Be(
            ImageFrameDecoderKind.ApngAnimation);
    }

    [Fact]
    public void Resolve_WithGifExtension_ReturnsAnimatedImageFrameDecoder()
    {
        ImageDecoderSelection decoderSelection =
            ((IImageDecoderResolver)_registry).Resolve(
                "image.gif");

        decoderSelection.FrameDecoderKind.Should().Be(
            ImageFrameDecoderKind.AnimatedImage);
    }

    [Fact]
    public void Resolve_WithWebpExtension_ReturnsSkiaAnimationFrameDecoder()
    {
        ImageDecoderSelection decoderSelection =
            ((IImageDecoderResolver)_registry).Resolve(
                "image.webp");

        decoderSelection.FrameDecoderKind.Should().Be(
            ImageFrameDecoderKind.SkiaAnimation);
    }

    [Theory]
    [InlineData("image.avif")]
    [InlineData("image.heic")]
    [InlineData("image.heif")]
    public void Resolve_WithHeifFamilyExtension_ReturnsMagickAnimationFrameDecoder(
        string fileName)
    {
        ImageDecoderSelection decoderSelection =
            ((IImageDecoderResolver)_registry).Resolve(fileName);

        decoderSelection.FrameDecoderKind.Should().Be(
            ImageFrameDecoderKind.MagickAnimation);
    }

    [Theory]
    [InlineData("image.gif", 2)]
    [InlineData("image.png", 2)]
    [InlineData("image.apng", 2)]
    [InlineData("image.webp", 8)]
    [InlineData("image.avif", 12)]
    [InlineData("image.heic", 12)]
    [InlineData("image.heif", 12)]
    public void Resolve_WithAnimatedExtension_ReturnsExpectedPlaybackBufferSize(
        string fileName,
        int expectedFrameCount)
    {
        ImageDecoderSelection decoderSelection =
            ((IImageDecoderResolver)_registry).Resolve(fileName);

        decoderSelection
            .EffectiveAnimationBufferingPolicy
            .PlaybackStartFrameCount
            .Should()
            .Be(expectedFrameCount);
    }

    [Theory]
    [InlineData("image.ico")]
    [InlineData("image.cur")]
    public void Resolve_WithIconExtension_SelectsLargestInitialFrame(
        string fileName)
    {
        ImageDecoderSelection decoderSelection =
            ((IImageDecoderResolver)_registry).Resolve(fileName);

        decoderSelection.InitialFrameSelection.Should().Be(
            ImageInitialFrameSelection.LargestArea);
        decoderSelection.FrameNumbering.Should().Be(
            ImageFrameNumbering.Reverse);
    }
}
