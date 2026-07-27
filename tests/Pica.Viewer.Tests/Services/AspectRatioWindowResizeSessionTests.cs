using Avalonia;
using FluentAssertions;
using Xunit;

using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.Services;

public sealed class AspectRatioWindowResizeSessionTests
{
    private const int TitleBarHeight = 36;
    private const double AspectRatio = 16d / 9d;

    [Fact]
    public void Calculate_WhenLeftEdgeMoves_PreservesRightEdgeAndContentAspectRatio()
    {
        WindowRectangle initialRectangle = CreateInitialRectangle();
        AspectRatioWindowResizeSession session = new(
            initialRectangle,
            new PixelPoint(100, 400),
            WindowSizingEdges.Left,
            0,
            TitleBarHeight,
            AspectRatio);

        WindowRectangle result = session.Calculate(new PixelPoint(200, 400));

        result.Right.Should().Be(initialRectangle.Right);
        GetContentAspectRatio(result).Should().BeApproximately(AspectRatio, 0.002d);
    }

    [Fact]
    public void Calculate_WhenCornerContinuesVertically_KeepsInitialSizingBasis()
    {
        WindowRectangle initialRectangle = CreateInitialRectangle();
        AspectRatioWindowResizeSession session = new(
            initialRectangle,
            new PixelPoint(900, 586),
            WindowSizingEdges.BottomRight,
            0,
            TitleBarHeight,
            AspectRatio);
        session.Calculate(new PixelPoint(920, 590));

        WindowRectangle result = session.Calculate(new PixelPoint(925, 680));

        result.Width.Should().Be(825);
        GetContentAspectRatio(result).Should().BeApproximately(AspectRatio, 0.002d);
    }

    private static WindowRectangle CreateInitialRectangle()
    {
        return new WindowRectangle
        {
            Left = 100,
            Top = 100,
            Right = 900,
            Bottom = 586
        };
    }

    private static double GetContentAspectRatio(WindowRectangle rectangle)
    {
        return rectangle.Width / (double)(rectangle.Height - TitleBarHeight);
    }
}
