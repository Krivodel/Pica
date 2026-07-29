using FluentAssertions;
using Xunit;

using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.Services;

public sealed class ImageFormatRegistryTests
{
    private readonly ImageFormatRegistry _registry = new();

    [Theory]
    [InlineData("image.png")]
    [InlineData("image.jpg")]
    [InlineData("image.jpeg")]
    [InlineData("image.webp")]
    [InlineData("image.bmp")]
    [InlineData("image.gif")]
    [InlineData("image.ico")]
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
    [InlineData("image.ico", typeof(AvaloniaBitmapDecoder))]
    [InlineData("image.avif", typeof(MagickImageDecoder))]
    [InlineData("image.heic", typeof(MagickImageDecoder))]
    [InlineData("image.heif", typeof(MagickImageDecoder))]
    [InlineData("image.tif", typeof(MagickImageDecoder))]
    [InlineData("image.tiff", typeof(MagickImageDecoder))]
    public void Resolve_WithSupportedExtension_ReturnsExpectedDecoder(
        string fileName,
        Type expectedDecoderType)
    {
        IImageDecoder decoder = ((IImageDecoderResolver)_registry).Resolve(fileName);

        decoder.GetType().Should().Be(expectedDecoderType);
    }
}
