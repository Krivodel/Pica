using Avalonia.Input;

namespace Pica.Viewer.Resources;

internal static class ViewerCursors
{
    internal static Cursor Arrow { get; } = new(StandardCursorType.Arrow);
    internal static Cursor Hidden { get; } = new(StandardCursorType.None);
    internal static Cursor Crosshair { get; } = new(StandardCursorType.Cross);
    internal static Cursor Move { get; } = new(StandardCursorType.SizeAll);
    internal static Cursor Hand { get; } = new(StandardCursorType.Hand);
    internal static Cursor HorizontalResize { get; } = new(StandardCursorType.SizeWestEast);
    internal static Cursor VerticalResize { get; } = new(StandardCursorType.SizeNorthSouth);
    internal static Cursor TopLeftResize { get; } = new(StandardCursorType.TopLeftCorner);
    internal static Cursor TopRightResize { get; } = new(StandardCursorType.TopRightCorner);
}
