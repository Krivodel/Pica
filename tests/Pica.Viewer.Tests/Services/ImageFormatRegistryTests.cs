using FluentAssertions;
using Xunit;

using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.Services;

public sealed class ImageFormatRegistryTests
{
    private readonly ImageFormatRegistry _registry = new();

    [Theory]
    [InlineData("image.avif")]
    [InlineData("image.heic")]
    [InlineData("image.heif")]
    [InlineData("image.tif")]
    [InlineData("image.tiff")]
    [InlineData("IMAGE.AVIF")]
    [InlineData("IMAGE.HEIC")]
    [InlineData("IMAGE.HEIF")]
    [InlineData("IMAGE.TIFF")]
    public void IsSupportedFileName_WithMagickExtension_ReturnsTrue(string fileName)
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
    [InlineData("image.avif")]
    [InlineData("image.heic")]
    [InlineData("image.heif")]
    [InlineData("image.tif")]
    [InlineData("image.tiff")]
    public void Resolve_WithMagickExtension_ReturnsMagickDecoder(string fileName)
    {
        IImageDecoder decoder = ((IImageDecoderResolver)_registry).Resolve(fileName);

        decoder.Should().BeOfType<MagickImageDecoder>();
    }
}
