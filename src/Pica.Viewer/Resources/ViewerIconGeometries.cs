using Avalonia.Media;

namespace Pica.Viewer.Resources;

internal static class ViewerIconGeometries
{
    internal static StreamGeometry CloseOrCancel { get; } =
        StreamGeometry.Parse(
            "M6,7.4 L7.4,6 L12,10.6 L16.6,6 L18,7.4 L13.4,12 L18,16.6 L16.6,18 L12,13.4 L7.4,18 L6,16.6 L10.6,12 Z");
    internal static StreamGeometry Submenu { get; } =
        StreamGeometry.Parse("M9,6 L15,12 L9,18 Z");
}
