using Avalonia;
using FluentAssertions;
using Xunit;

using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.Services;

public sealed class ImageWindowGeometryTests
{
    [Fact]
    public void FitImage_WithLandscapeImage_PreservesAspectRatio()
    {
        PixelSize imageSize = new(1920, 1080);

        AssertFittedSize(imageSize, new Size(1200d, 675d));
    }

    [Fact]
    public void FitImage_WithPortraitImage_UsesSamePreferredExtent()
    {
        PixelSize imageSize = new(1080, 1920);

        AssertFittedSize(imageSize, new Size(506.25d, 900d));
    }

    [Fact]
    public void CalculateFittedScale_WithImageSmallerThanViewport_ReturnsUpscalingFactor()
    {
        PixelSize imageSize = new(320, 180);
        Size availableSize = new(1920d, 1080d);

        double result = ImageWindowGeometry.CalculateFittedScale(imageSize, availableSize);

        result.Should().Be(6d);
    }

    [Fact]
    public void GetPanBounds_WithImageSmallerThanViewport_FixesOffsetAtCenter()
    {
        Size imageSize = new(320d, 180d);
        Size viewportSize = new(1920d, 1080d);

        Rect result = ImageWindowGeometry.GetPanBounds(imageSize, viewportSize);

        result.Should().Be(new Rect(800d, 450d, 0d, 0d));
    }

    private static void AssertFittedSize(PixelSize imageSize, Size expectedSize)
    {
        Size maximumSize = new(1400d, 900d);

        Size result = ImageWindowGeometry.FitImage(imageSize, 1200d, maximumSize);

        result.Width.Should().BeApproximately(expectedSize.Width, 0.001d);
        result.Height.Should().BeApproximately(expectedSize.Height, 0.001d);
    }
}
