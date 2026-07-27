namespace Pica.Viewer.Services;

internal static class WindowSizingEdgesExtensions
{
    public static bool IncludesHorizontalEdge(this WindowSizingEdges sizingEdges)
    {
        return sizingEdges.HasFlag(WindowSizingEdges.Left)
            || sizingEdges.HasFlag(WindowSizingEdges.Right);
    }

    public static bool IncludesVerticalEdge(this WindowSizingEdges sizingEdges)
    {
        return sizingEdges.HasFlag(WindowSizingEdges.Top)
            || sizingEdges.HasFlag(WindowSizingEdges.Bottom);
    }
}
