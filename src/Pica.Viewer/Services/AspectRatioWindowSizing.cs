namespace Pica.Viewer.Services;

internal static class AspectRatioWindowSizing
{
    private const int MinimumClientExtent = 64;

    public static WindowRectangle Constrain(
        WindowRectangle requestedRectangle,
        WindowSizingEdges sizingEdges,
        WindowSizingBasis sizingBasis,
        int frameWidth,
        int frameHeight,
        double aspectRatio)
    {
        if ((aspectRatio <= 0d)
            || !double.IsFinite(aspectRatio))
        {
            return requestedRectangle;
        }

        int requestedClientWidth = Math.Max(1, requestedRectangle.Width - frameWidth);
        int requestedClientHeight = Math.Max(1, requestedRectangle.Height - frameHeight);
        int targetClientWidth;
        int targetClientHeight;

        if (sizingBasis == WindowSizingBasis.Width)
        {
            targetClientWidth = Math.Max(MinimumClientExtent, requestedClientWidth);
            targetClientHeight = Math.Max(1, (int)Math.Round(targetClientWidth / aspectRatio));
        }
        else
        {
            targetClientHeight = Math.Max(MinimumClientExtent, requestedClientHeight);
            targetClientWidth = Math.Max(1, (int)Math.Round(targetClientHeight * aspectRatio));
        }

        int targetWindowWidth = targetClientWidth + frameWidth;
        int targetWindowHeight = targetClientHeight + frameHeight;
        WindowRectangle result = requestedRectangle;
        ApplyHorizontalEdge(ref result, sizingEdges, targetWindowWidth);
        ApplyVerticalEdge(ref result, sizingEdges, targetWindowHeight);

        return result;
    }

    private static void ApplyHorizontalEdge(
        ref WindowRectangle rectangle,
        WindowSizingEdges sizingEdges,
        int targetWidth)
    {
        if (sizingEdges.HasFlag(WindowSizingEdges.Left))
        {
            rectangle.Left = rectangle.Right - targetWidth;
            return;
        }

        rectangle.Right = rectangle.Left + targetWidth;
    }

    private static void ApplyVerticalEdge(
        ref WindowRectangle rectangle,
        WindowSizingEdges sizingEdges,
        int targetHeight)
    {
        if (sizingEdges.HasFlag(WindowSizingEdges.Top))
        {
            rectangle.Top = rectangle.Bottom - targetHeight;
            return;
        }

        rectangle.Bottom = rectangle.Top + targetHeight;
    }
}
