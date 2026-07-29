using FluentAssertions;
using SkiaSharp;
using Xunit;

using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.Services;

public sealed class AvaloniaBitmapDecoderTests
{
    [Fact]
    public void ReadHasAlpha_WithRgbaPng_ReturnsTrue()
    {
        using MemoryStream stream = CreatePngStream(SKAlphaType.Unpremul);
        AvaloniaBitmapDecoder decoder = new();

        bool hasAlpha = decoder.ReadHasAlpha(
            stream,
            CancellationToken.None);

        hasAlpha.Should().BeTrue();
    }

    [Fact]
    public void ReadHasAlpha_WithOpaquePng_ReturnsFalse()
    {
        using MemoryStream stream = CreatePngStream(SKAlphaType.Opaque);
        AvaloniaBitmapDecoder decoder = new();

        bool hasAlpha = decoder.ReadHasAlpha(
            stream,
            CancellationToken.None);

        hasAlpha.Should().BeFalse();
    }

    private static MemoryStream CreatePngStream(SKAlphaType alphaType)
    {
        SKImageInfo imageInfo = new(
            1,
            1,
            SKColorType.Bgra8888,
            alphaType);
        using SKBitmap bitmap = new(imageInfo);
        bitmap.SetPixel(0, 0, new SKColor(30, 20, 10, 128));
        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(
            SKEncodedImageFormat.Png,
            100);

        return new MemoryStream(data.ToArray());
    }
}
