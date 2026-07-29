using Avalonia;
using FluentAssertions;
using Xunit;

using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.Services;

public sealed class ImageChannelBitmapLoaderTests
{
    private const int RowBytes = 8;

    [Fact]
    public void ApplyChannel_WithRedChannel_CreatesOpaqueGrayscalePixels()
    {
        PreparedBitmapPixels pixels = CreatePixels();

        ImageChannelBitmapLoader.ApplyChannel(
            pixels,
            ImageChannel.Red,
            CancellationToken.None);

        pixels.BgraPixels.Should().Equal(
            30, 30, 30, 255,
            60, 60, 60, 255);
    }

    [Fact]
    public void ApplyChannel_WithAlphaChannel_CreatesOpaqueAlphaVisualization()
    {
        PreparedBitmapPixels pixels = CreatePixels();

        ImageChannelBitmapLoader.ApplyChannel(
            pixels,
            ImageChannel.Alpha,
            CancellationToken.None);

        pixels.BgraPixels.Should().Equal(
            128, 128, 128, 255,
            255, 255, 255, 255);
    }

    private static PreparedBitmapPixels CreatePixels()
    {
        byte[] pixels =
        [
            10, 20, 30, 128,
            40, 50, 60, 255
        ];

        return new PreparedBitmapPixels(
            new PixelSize(2, 1),
            RowBytes,
            pixels);
    }
}
