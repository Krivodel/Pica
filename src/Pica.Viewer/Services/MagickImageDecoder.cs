using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ImageMagick;

namespace Pica.Viewer.Services;

internal sealed class MagickImageDecoder : IImageDecoder
{
    public PixelSize ReadPixelSize(Stream sourceStream, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sourceStream);
        ct.ThrowIfCancellationRequested();
        MagickImageInfo imageInfo = new(sourceStream);
        ct.ThrowIfCancellationRequested();
        int width = checked((int)imageInfo.Width);
        int height = checked((int)imageInfo.Height);
        bool swapDimensions = imageInfo.Orientation is OrientationType.LeftTop
            or OrientationType.RightTop
            or OrientationType.RightBottom
            or OrientationType.LeftBottom;

        return swapDimensions
            ? new PixelSize(height, width)
            : new PixelSize(width, height);
    }

    public bool ReadHasAlpha(Stream sourceStream, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sourceStream);
        ct.ThrowIfCancellationRequested();
        using MagickImage image = new(sourceStream);
        ct.ThrowIfCancellationRequested();

        return image.HasAlpha;
    }

    public Bitmap Decode(Stream sourceStream, CancellationToken ct)
    {
        using MagickImage image = ReadImage(sourceStream, ct);

        return CreateBitmap(image, ct);
    }

    public Bitmap DecodeToWidth(Stream sourceStream, int width, CancellationToken ct)
    {
        using MagickImage image = ReadImage(sourceStream, ct);
        ResizeToWidth(image, width, ct);

        return CreateBitmap(image, ct);
    }

    internal static Bitmap CreateBitmap(
        IMagickImage<byte> image,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        PixelSize pixelSize = new(
            checked((int)image.Width),
            checked((int)image.Height));
        using IPixelCollection<byte> pixelCollection = image.GetPixels();
        byte[]? exportedPixels = pixelCollection.ToByteArray(PixelMapping.BGRA);

        if (exportedPixels is null)
        {
            throw new InvalidDataException("The image decoder did not return a pixel buffer.");
        }

        ct.ThrowIfCancellationRequested();
        return BgraBitmapFactory.Create(
            pixelSize,
            exportedPixels,
            AlphaFormat.Unpremul,
            ct);
    }

    internal static void ResizeToWidth(
        IMagickImage<byte> image,
        int width,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(width),
                width,
                "The decoded image width must be positive.");
        }

        uint height = CalculateScaledHeight(
            image.Width,
            image.Height,
            width);
        image.Resize(checked((uint)width), height);
        ct.ThrowIfCancellationRequested();
    }

    private static uint CalculateScaledHeight(
        uint sourceWidth,
        uint sourceHeight,
        int targetWidth)
    {
        if ((sourceWidth == 0) || (sourceHeight == 0))
        {
            throw new InvalidDataException(
                "The image dimensions must be positive.");
        }

        double scaledHeight =
            (double)sourceHeight
            * targetWidth
            / sourceWidth;

        return checked((uint)Math.Max(
            1d,
            Math.Round(scaledHeight)));
    }

    private static MagickImage ReadImage(
        Stream sourceStream,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sourceStream);
        ct.ThrowIfCancellationRequested();
        MagickImage image = new(sourceStream);

        try
        {
            image.AutoOrient();
            ct.ThrowIfCancellationRequested();

            return image;
        }
        catch
        {
            image.Dispose();
            throw;
        }
    }
}
