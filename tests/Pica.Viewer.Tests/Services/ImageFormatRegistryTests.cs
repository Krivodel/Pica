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
    public void Resolve_WithHeicExtension_ReturnsHeicDecoder()
    {
        IImageDecoder decoder = ((IImageDecoderResolver)_registry).Resolve("image.heic");

        decoder.Should().BeOfType<MagickHeicImageDecoder>();
    }
}
