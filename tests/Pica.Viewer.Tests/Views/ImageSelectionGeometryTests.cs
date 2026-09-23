using Avalonia;
using FluentAssertions;
using Xunit;

using Pica.Viewer.Views;

namespace Pica.Viewer.Tests.Views;

public sealed class ImageSelectionGeometryTests
{
    [Theory]
    [InlineData((int)SelectionResizeModes.Left, 75, 50, 60, 30, 15, 50)]
    [InlineData((int)SelectionResizeModes.Right, 10, 50, 10, 30, 10, 50)]
    [InlineData((int)SelectionResizeModes.Top, 40, 95, 20, 80, 40, 15)]
    [InlineData((int)SelectionResizeModes.Bottom, 40, 15, 20, 15, 40, 15)]
    [InlineData((int)SelectionResizeModes.TopLeft, 75, 95, 60, 80, 15, 15)]
    [InlineData((int)SelectionResizeModes.TopRight, 10, 95, 10, 80, 10, 15)]
    [InlineData((int)SelectionResizeModes.BottomRight, 10, 15, 10, 15, 10, 15)]
    [InlineData((int)SelectionResizeModes.BottomLeft, 75, 15, 60, 15, 15, 15)]
    [InlineData((int)SelectionResizeModes.TopLeft, 75, 40, 60, 40, 15, 40)]
    [InlineData((int)SelectionResizeModes.Left, 61, 50, 60, 30, 1, 50)]
    [InlineData((int)SelectionResizeModes.Right, 19, 50, 19, 30, 1, 50)]
    [InlineData((int)SelectionResizeModes.Left, 60, 50, 59, 30, 1, 50)]
    [InlineData((int)SelectionResizeModes.Right, 20, 50, 20, 30, 1, 50)]
    [InlineData((int)SelectionResizeModes.Top, 40, 80, 20, 79, 40, 1)]
    [InlineData((int)SelectionResizeModes.Bottom, 40, 30, 20, 30, 40, 1)]
    [InlineData((int)SelectionResizeModes.TopLeft, 60, 80, 59, 79, 1, 1)]
    [InlineData((int)SelectionResizeModes.Left, -100, 50, 0, 30, 60, 50)]
    [InlineData((int)SelectionResizeModes.Left, 1000, 50, 60, 30, 40, 50)]
    public void ResizePixelRect_WhenHandleMoves_NormalizesAndClampsSelection(
        int resizeMode,
        int pointerX,
        int pointerY,
        int expectedX,
        int expectedY,
        int expectedWidth,
        int expectedHeight)
    {
        PixelRect initialRect = new(20, 30, 40, 50);
        PixelPoint pointerBoundary = new(pointerX, pointerY);
        PixelSize imageSize = new(100, 120);

        PixelRect result = ImageSelectionGeometry.ResizePixelRect(
            initialRect,
            (SelectionResizeModes)resizeMode,
            pointerBoundary,
            imageSize);

        result.Should().Be(new PixelRect(
            expectedX,
            expectedY,
            expectedWidth,
            expectedHeight));
    }

    [Fact]
    public void ResizePixelRect_WhenPointerReturnsAcrossAnchor_RestoresOriginalSide()
    {
        PixelRect initialRect = new(20, 30, 40, 50);
        PixelSize imageSize = new(100, 120);

        PixelRect crossed = ImageSelectionGeometry.ResizePixelRect(
            initialRect,
            SelectionResizeModes.Left,
            new PixelPoint(75, 50),
            imageSize);
        PixelRect returned = ImageSelectionGeometry.ResizePixelRect(
            initialRect,
            SelectionResizeModes.Left,
            new PixelPoint(45, 50),
            imageSize);

        crossed.Should().Be(new PixelRect(60, 30, 15, 50));
        returned.Should().Be(new PixelRect(45, 30, 15, 50));
    }
}
