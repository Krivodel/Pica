using Avalonia;

namespace Pica.Viewer.Services;

internal abstract class WindowResizeSession : IWindowResizeSession
{
    protected WindowRectangle InitialRectangle { get; }
    protected PixelPoint InitialPointerPosition { get; }
    protected WindowSizingEdges SizingEdges { get; }

    protected WindowResizeSession(
        WindowRectangle initialRectangle,
        PixelPoint initialPointerPosition,
        WindowSizingEdges sizingEdges)
    {
        InitialRectangle = initialRectangle;
        InitialPointerPosition = initialPointerPosition;
        SizingEdges = sizingEdges;
    }

    public WindowRectangle Calculate(PixelPoint pointerPosition)
    {
        int horizontalDelta = pointerPosition.X - InitialPointerPosition.X;
        int verticalDelta = pointerPosition.Y - InitialPointerPosition.Y;

        return CalculateCore(horizontalDelta, verticalDelta);
    }

    protected abstract WindowRectangle CalculateCore(int horizontalDelta, int verticalDelta);
}
