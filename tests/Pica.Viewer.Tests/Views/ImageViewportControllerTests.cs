using Avalonia;
using FluentAssertions;
using Xunit;

using Pica.Viewer.Views;

namespace Pica.Viewer.Tests.Views;

public sealed class ImageViewportControllerTests
{
    [Fact]
    public void CalculateCheckerboardPatternOffsetAfterPan_WhenImageMoves_PreservesPatternScreenPosition()
    {
        Vector currentPatternOffset = new(3d, -7d);
        Point previousImageOffset = new(120d, 80d);
        Point currentImageOffset = new(95d, 125d);

        Vector result =
            ImageViewportController
                .CalculateCheckerboardPatternOffsetAfterPan(
                    currentPatternOffset,
                    previousImageOffset,
                    currentImageOffset);

        Point previousPatternScreenPosition =
            previousImageOffset + currentPatternOffset;
        Point currentPatternScreenPosition =
            currentImageOffset + result;
        currentPatternScreenPosition.Should().Be(
            previousPatternScreenPosition);
    }
}
