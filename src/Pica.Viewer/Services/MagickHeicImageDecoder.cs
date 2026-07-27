using System.Runtime.InteropServices;

using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ImageMagick;

namespace Pica.Viewer.Services;

internal sealed class MagickHeicImageDecoder : IImageDecoder
{
    private const int BytesPerPixel = 4;
    private const double DefaultDpi = 96d;

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

    public Bitmap Decode(Stream sourceStream, CancellationToken ct)
    {
        using MagickImage image = ReadImage(sourceStream, ct);

        return CreateBitmap(image, ct);
    }

    public Bitmap DecodeToWidth(Stream sourceStream, int width, CancellationToken ct)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(width),
                width,
                "The decoded image width must be positive.");
        }

        using MagickImage image = ReadImage(sourceStream, ct);
        uint height = CalculateScaledHeight(image.Width, image.Height, width);
        image.Resize(checked((uint)width), height);
        ct.ThrowIfCancellationRequested();

        return CreateBitmap(image, ct);
    }

    private static uint CalculateScaledHeight(uint sourceWidth, uint sourceHeight, int targetWidth)
    {
        if ((sourceWidth == 0) || (sourceHeight == 0))
        {
            throw new InvalidDataException("The HEIC image dimensions must be positive.");
        }

        double scaledHeight = (double)sourceHeight * targetWidth / sourceWidth;

        return checked((uint)Math.Max(1d, Math.Round(scaledHeight)));
    }

    private static MagickImage ReadImage(Stream sourceStream, CancellationToken ct)
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

    private static Bitmap CreateBitmap(MagickImage image, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        PixelSize pixelSize = new(
            checked((int)image.Width),
            checked((int)image.Height));
        using IPixelCollection<byte> pixelCollection = image.GetPixels();
        byte[]? exportedPixels = pixelCollection.ToByteArray(PixelMapping.BGRA);

        if (exportedPixels is null)
        {
            throw new InvalidDataException("The HEIC decoder did not return a pixel buffer.");
        }

        ct.ThrowIfCancellationRequested();
        WriteableBitmap bitmap = new(
            pixelSize,
            new Vector(DefaultDpi, DefaultDpi),
            PixelFormat.Bgra8888,
            AlphaFormat.Unpremul);

        try
        {
            CopyPixels(bitmap, exportedPixels, ct);

            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    private static void CopyPixels(
        WriteableBitmap bitmap,
        byte[] source,
        CancellationToken ct)
    {
        int sourceRowBytes = checked(bitmap.PixelSize.Width * BytesPerPixel);
        int expectedLength = checked(sourceRowBytes * bitmap.PixelSize.Height);

        if (source.Length != expectedLength)
        {
            throw new InvalidDataException(
                $"The decoded HEIC pixel buffer has length {source.Length}, expected {expectedLength}.");
        }

        using ILockedFramebuffer framebuffer = bitmap.Lock();

        if (framebuffer.RowBytes == sourceRowBytes)
        {
            Marshal.Copy(source, 0, framebuffer.Address, source.Length);
            ct.ThrowIfCancellationRequested();
            return;
        }

        for (int row = 0; row < framebuffer.Size.Height; row++)
        {
            ct.ThrowIfCancellationRequested();
            IntPtr destinationAddress = IntPtr.Add(
                framebuffer.Address,
                row * framebuffer.RowBytes);
            Marshal.Copy(
                source,
                row * sourceRowBytes,
                destinationAddress,
                sourceRowBytes);
        }
    }
}
