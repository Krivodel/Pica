using FluentAssertions;
using Xunit;

using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.Services;

public sealed class ImageFormatRegistryTests
{
    private readonly ImageFormatRegistry _registry = new();

    [Theory]
    [InlineData("image.heic")]
    [InlineData("IMAGE.HEIC")]
    public void IsSupportedFileName_WithHeicExtension_ReturnsTrue(string fileName)
    {
        bool isSupported = _registry.IsSupportedFileName(fileName);

        isSupported.Should().BeTrue();
    }

    [Fact]
    public void GetContentType_WithHeicExtension_ReturnsHeicContentType()
    {
        string contentType = _registry.GetContentType("image.heic");

        contentType.Should().Be(PicaImageFormats.HeicContentType);
    }

    [Fact]
    public void Resolve_WithHeicExtension_ReturnsMagickDecoder()
    {
        IImageDecoder decoder = ((IImageDecoderResolver)_registry).Resolve("image.heic");

        decoder.Should().BeOfType<MagickImageDecoder>();
    }

    [Theory]
    [InlineData("image.tif")]
    [InlineData("image.tiff")]
    [InlineData("IMAGE.TIFF")]
    public void IsSupportedFileName_WithTiffExtension_ReturnsTrue(string fileName)
    {
        bool isSupported = _registry.IsSupportedFileName(fileName);

        isSupported.Should().BeTrue();
    }

    [Theory]
    [InlineData("image.tif")]
    [InlineData("image.tiff")]
    public void GetContentType_WithTiffExtension_ReturnsTiffContentType(string fileName)
    {
        string contentType = _registry.GetContentType(fileName);

        contentType.Should().Be(PicaImageFormats.TiffContentType);
    }

    [Theory]
    [InlineData("image.tif")]
    [InlineData("image.tiff")]
    public void Resolve_WithTiffExtension_ReturnsMagickDecoder(string fileName)
    {
        IImageDecoder decoder = ((IImageDecoderResolver)_registry).Resolve(fileName);

        decoder.Should().BeOfType<MagickImageDecoder>();
    }
}
