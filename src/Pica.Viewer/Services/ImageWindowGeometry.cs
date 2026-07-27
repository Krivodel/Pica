using Avalonia;

namespace Pica.Viewer.Services;

internal static class ImageWindowGeometry
{
    public static Size FitImage(PixelSize imageSize, double preferredExtent, Size maximumSize)
    {
        ValidateImageSize(imageSize);
        ValidateSize(maximumSize, nameof(maximumSize));

        double boundedExtent = Math.Max(1d, preferredExtent);
        double availableWidth = Math.Min(boundedExtent, maximumSize.Width);
        double availableHeight = Math.Min(boundedExtent, maximumSize.Height);
        double scale = CalculateFittedScale(
            imageSize,
            new Size(availableWidth, availableHeight));

        return new Size(imageSize.Width * scale, imageSize.Height * scale);
    }

    public static double CalculateFittedScale(PixelSize imageSize, Size availableSize)
    {
        ValidateImageSize(imageSize);
        ValidateSize(availableSize, nameof(availableSize));

        return Math.Min(
            availableSize.Width / imageSize.Width,
            availableSize.Height / imageSize.Height);
    }

    public static Rect GetPanBounds(Size imageSize, Size viewportSize)
    {
        ValidateSize(imageSize, nameof(imageSize));
        ValidateSize(viewportSize, nameof(viewportSize));

        double minimumX = imageSize.Width <= viewportSize.Width
            ? (viewportSize.Width - imageSize.Width) / 2d
            : viewportSize.Width - imageSize.Width;
        double maximumX = imageSize.Width <= viewportSize.Width
            ? minimumX
            : 0d;
        double minimumY = imageSize.Height <= viewportSize.Height
            ? (viewportSize.Height - imageSize.Height) / 2d
            : viewportSize.Height - imageSize.Height;
        double maximumY = imageSize.Height <= viewportSize.Height
            ? minimumY
            : 0d;

        return new Rect(
            minimumX,
            minimumY,
            maximumX - minimumX,
            maximumY - minimumY);
    }

    public static Point ClampOffset(Point offset, Rect bounds)
    {
        return new Point(
            Math.Clamp(offset.X, bounds.Left, bounds.Right),
            Math.Clamp(offset.Y, bounds.Top, bounds.Bottom));
    }

    private static void ValidateImageSize(PixelSize imageSize)
    {
        if ((imageSize.Width <= 0) || (imageSize.Height <= 0))
        {
            throw new ArgumentOutOfRangeException(nameof(imageSize));
        }
    }

    private static void ValidateSize(Size size, string parameterName)
    {
        if ((size.Width <= 0d)
            || (size.Height <= 0d)
            || !double.IsFinite(size.Width)
            || !double.IsFinite(size.Height))
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
