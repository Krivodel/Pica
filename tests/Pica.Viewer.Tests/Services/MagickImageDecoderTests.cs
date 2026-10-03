using FluentAssertions;
using ImageMagick;
using Xunit;

using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.Services;

public sealed class MagickImageDecoderTests
{
    [Theory]
    [InlineData(24, false)]
    [InlineData(32, true)]
    public void ReadHasAlpha_WithTga_ReturnsPixelDepthAlpha(int pixelDepth, bool expectedAlpha)
    {
        using MemoryStream stream = new(TgaImageTestData.Create(pixelDepth, true, 0x20));
        IImageDecoder decoder = ((IImageDecoderResolver)new ImageFormatRegistry())
            .Resolve("image" + PicaImageFormats.TgaExtension).Decoder;

        bool hasAlpha = decoder.ReadHasAlpha(stream, CancellationToken.None);

        hasAlpha.Should().Be(expectedAlpha);
    }

    [Fact]
    public void ReadHasAlpha_WithRgbaTiff_ReturnsTrue()
    {
        using MemoryStream stream = CreateTiffStream(MagickColors.Transparent);
        MagickImageDecoder decoder = new();

        bool hasAlpha = decoder.ReadHasAlpha(
            stream,
            CancellationToken.None);

        hasAlpha.Should().BeTrue();
    }

    [Fact]
    public void ReadHasAlpha_WithOpaqueTiff_ReturnsFalse()
    {
        using MemoryStream stream = CreateTiffStream(MagickColors.Red);
        MagickImageDecoder decoder = new();

        bool hasAlpha = decoder.ReadHasAlpha(
            stream,
            CancellationToken.None);

        hasAlpha.Should().BeFalse();
    }

    private static MemoryStream CreateTiffStream(IMagickColor<byte> color)
    {
        MemoryStream stream = new();
        using MagickImage image = new(color, 1, 1);
        image.Format = MagickFormat.Tiff;
        image.Write(stream);
        stream.Position = 0;

        return stream;
    }
}
