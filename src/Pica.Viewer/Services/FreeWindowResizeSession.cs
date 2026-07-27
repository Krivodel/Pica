using Avalonia;

namespace Pica.Viewer.Services;

internal sealed class FreeWindowResizeSession : WindowResizeSession
{
    private const int MinimumWindowExtent = 64;

    public FreeWindowResizeSession(
        WindowRectangle initialRectangle,
        PixelPoint initialPointerPosition,
        WindowSizingEdges sizingEdges)
        : base(initialRectangle, initialPointerPosition, sizingEdges)
    {
    }

    protected override WindowRectangle CalculateCore(int horizontalDelta, int verticalDelta)
    {
        WindowRectangle rectangle = InitialRectangle;
        ApplyHorizontalDelta(ref rectangle, horizontalDelta);
        ApplyVerticalDelta(ref rectangle, verticalDelta);

        return rectangle;
    }

    private void ApplyHorizontalDelta(ref WindowRectangle rectangle, int delta)
    {
        if (SizingEdges.HasFlag(WindowSizingEdges.Left))
        {
            rectangle.Left = Math.Min(
                rectangle.Left + delta,
                rectangle.Right - MinimumWindowExtent);
        }
        else if (SizingEdges.HasFlag(WindowSizingEdges.Right))
        {
            rectangle.Right = Math.Max(
                rectangle.Right + delta,
                rectangle.Left + MinimumWindowExtent);
        }
    }

    private void ApplyVerticalDelta(ref WindowRectangle rectangle, int delta)
    {
        if (SizingEdges.HasFlag(WindowSizingEdges.Top))
        {
            rectangle.Top = Math.Min(
                rectangle.Top + delta,
                rectangle.Bottom - MinimumWindowExtent);
        }
        else if (SizingEdges.HasFlag(WindowSizingEdges.Bottom))
        {
            rectangle.Bottom = Math.Max(
                rectangle.Bottom + delta,
                rectangle.Top + MinimumWindowExtent);
        }
    }
}
