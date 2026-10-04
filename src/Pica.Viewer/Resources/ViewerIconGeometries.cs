using Avalonia.Media;

namespace Pica.Viewer.Resources;

internal static class ViewerIconGeometries
{
    internal static StreamGeometry CloseOrCancel { get; } =
        StreamGeometry.Parse(
            "M6,7.4 L7.4,6 L12,10.6 L16.6,6 L18,7.4 L13.4,12 L18,16.6 L16.6,18 L12,13.4 L7.4,18 L6,16.6 L10.6,12 Z");
    internal static StreamGeometry Settings { get; } =
        StreamGeometry.Parse(
            "M12,8.4 A3.6,3.6 0 1 0 12,15.6 A3.6,3.6 0 1 0 12,8.4 M12,2 L14,2.7 L14.4,5 L16.3,6.1 L18.5,5.3 L20.5,8.7 L18.8,10.2 L18.8,12.4 L20.5,13.9 L18.5,17.3 L16.3,16.5 L14.4,17.6 L14,20 L10,20 L9.6,17.6 L7.7,16.5 L5.5,17.3 L3.5,13.9 L5.2,12.4 L5.2,10.2 L3.5,8.7 L5.5,5.3 L7.7,6.1 L9.6,5 L10,2 Z");
    internal static StreamGeometry Submenu { get; } =
        StreamGeometry.Parse("M9,6 L15,12 L9,18 Z");
}
