using FluentAssertions;
using Xunit;

using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.Services;

public sealed class AspectRatioWindowSizingTests
{
    private const int FrameWidth = 16;
    private const int FrameHeight = 39;
    private const double AspectRatio = 16d / 9d;

    [Fact]
    public void Constrain_WhenRightEdgeMoves_PreservesLeftEdgeAndAspectRatio()
    {
        WindowRectangle requested = CreateRectangle(100, 100, 1116, 609);

        WindowRectangle result = AspectRatioWindowSizing.Constrain(
            requested,
            WindowSizingEdges.Right,
            WindowSizingBasis.Width,
            FrameWidth,
            FrameHeight,
            AspectRatio);

        result.Left.Should().Be(100);
        GetClientAspectRatio(result).Should().BeApproximately(AspectRatio, 0.002d);
    }

    [Fact]
    public void Constrain_WhenLeftEdgeMoves_PreservesRightEdge()
    {
        WindowRectangle requested = CreateRectangle(-100, 100, 916, 609);

        WindowRectangle result = AspectRatioWindowSizing.Constrain(
            requested,
            WindowSizingEdges.Left,
            WindowSizingBasis.Width,
            FrameWidth,
            FrameHeight,
            AspectRatio);

        result.Right.Should().Be(916);
        GetClientAspectRatio(result).Should().BeApproximately(AspectRatio, 0.002d);
    }

    [Fact]
    public void Constrain_WhenTopEdgeMoves_PreservesBottomEdgeAndAspectRatio()
    {
        WindowRectangle requested = CreateRectangle(100, -100, 916, 589);

        WindowRectangle result = AspectRatioWindowSizing.Constrain(
            requested,
            WindowSizingEdges.Top,
            WindowSizingBasis.Height,
            FrameWidth,
            FrameHeight,
            AspectRatio);

        result.Bottom.Should().Be(589);
        GetClientAspectRatio(result).Should().BeApproximately(AspectRatio, 0.002d);
    }

    private static WindowRectangle CreateRectangle(int left, int top, int right, int bottom)
    {
        return new WindowRectangle
        {
            Left = left,
            Top = top,
            Right = right,
            Bottom = bottom
        };
    }

    private static double GetClientAspectRatio(WindowRectangle rectangle)
    {
        return (rectangle.Width - FrameWidth) / (double)(rectangle.Height - FrameHeight);
    }
}
