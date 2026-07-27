using FluentAssertions;
using Xunit;

using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.Services;

public sealed class WindowSizingEdgesExtensionsTests
{
    public static TheoryData<int, bool> HorizontalEdgeExpectations =>
        CreateTheoryData(expectation => expectation.Horizontal);
    public static TheoryData<int, bool> VerticalEdgeExpectations =>
        CreateTheoryData(expectation => expectation.Vertical);

    private static readonly IReadOnlyList<(
        WindowSizingEdges Edges,
        bool Horizontal,
        bool Vertical)> EdgeExpectations =
        new List<(WindowSizingEdges Edges, bool Horizontal, bool Vertical)>
        {
            (WindowSizingEdges.Left, true, false),
            (WindowSizingEdges.Right, true, false),
            (WindowSizingEdges.Top, false, true),
            (WindowSizingEdges.Bottom, false, true),
            (WindowSizingEdges.TopLeft, true, true),
            (WindowSizingEdges.TopRight, true, true),
            (WindowSizingEdges.BottomLeft, true, true),
            (WindowSizingEdges.BottomRight, true, true)
        };

    [Theory]
    [MemberData(nameof(HorizontalEdgeExpectations))]
    public void IncludesHorizontalEdge_WithSizingEdges_ReturnsExpectedResult(
        int sizingEdgesValue,
        bool expectedResult)
    {
        AssertIncludesEdge(
            sizingEdgesValue,
            expectedResult,
            sizingEdges => sizingEdges.IncludesHorizontalEdge());
    }

    [Theory]
    [MemberData(nameof(VerticalEdgeExpectations))]
    public void IncludesVerticalEdge_WithSizingEdges_ReturnsExpectedResult(
        int sizingEdgesValue,
        bool expectedResult)
    {
        AssertIncludesEdge(
            sizingEdgesValue,
            expectedResult,
            sizingEdges => sizingEdges.IncludesVerticalEdge());
    }

    private static TheoryData<int, bool> CreateTheoryData(
        Func<(WindowSizingEdges Edges, bool Horizontal, bool Vertical), bool> selectExpected)
    {
        TheoryData<int, bool> theoryData = new();

        foreach ((
            WindowSizingEdges Edges,
            bool Horizontal,
            bool Vertical) expectation in EdgeExpectations)
        {
            theoryData.Add((int)expectation.Edges, selectExpected(expectation));
        }

        return theoryData;
    }

    private static void AssertIncludesEdge(
        int sizingEdgesValue,
        bool expectedResult,
        Func<WindowSizingEdges, bool> includesEdge)
    {
        WindowSizingEdges sizingEdges = (WindowSizingEdges)sizingEdgesValue;

        bool result = includesEdge(sizingEdges);

        result.Should().Be(expectedResult);
    }
}
