using Avalonia.Platform;
using FluentAssertions;
using Xunit;

using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.Services;

public sealed class BitmapPixelReaderTests
{
    [Fact]
    public void ConvertToBgra_WithRgbaPixels_SwapsRedAndBlueComponents()
    {
        byte[] pixels =
        [
            30, 20, 10, 255,
            60, 50, 40, 128
        ];

        BitmapPixelReader.ConvertToBgra(PixelFormat.Rgba8888, pixels);

        pixels.Should().Equal(
            10, 20, 30, 255,
            40, 50, 60, 128);
    }
}
