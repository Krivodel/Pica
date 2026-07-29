using Avalonia;
using Avalonia.Controls;

namespace Pica.Viewer.Views;

internal static class ViewerFloatingMenuPositioning
{
    internal static Size GetMeasuredSize(
        Border menu,
        Size fallbackSize)
    {
        ArgumentNullException.ThrowIfNull(menu);

        double width = menu.DesiredSize.Width;
        double height = menu.DesiredSize.Height;

        if (double.IsNaN(width)
            || (width <= 0d)
            || double.IsInfinity(width))
        {
            width = fallbackSize.Width;
        }

        if (double.IsNaN(height)
            || (height <= 0d)
            || double.IsInfinity(height))
        {
            height = fallbackSize.Height;
        }

        return new Size(width, height);
    }

    internal static Point CalculateContextPosition(
        Point pointerPosition,
        Size menuSize,
        Size viewport,
        double gap)
    {
        double maxX = Math.Max(0d, viewport.Width - menuSize.Width);
        double maxY = Math.Max(0d, viewport.Height - menuSize.Height);
        double x = pointerPosition.X + gap;
        double y = pointerPosition.Y + gap;

        if (x > maxX)
        {
            x = pointerPosition.X - menuSize.Width - gap;
        }

        if (y > maxY)
        {
            y = pointerPosition.Y - menuSize.Height - gap;
        }

        return new Point(
            Math.Clamp(x, 0d, maxX),
            Math.Clamp(y, 0d, maxY));
    }

    internal static Point CalculateToolPosition(
        Point anchorPosition,
        Size anchorSize,
        Size menuSize,
        Size viewport,
        double gap)
    {
        double maxX = Math.Max(0d, viewport.Width - menuSize.Width);
        double maxY = Math.Max(0d, viewport.Height - menuSize.Height);
        double x =
            anchorPosition.X + anchorSize.Width - menuSize.Width;
        double y = anchorPosition.Y - menuSize.Height - gap;

        return new Point(
            Math.Clamp(x, 0d, maxX),
            Math.Clamp(y, 0d, maxY));
    }
}
