using Avalonia;
using Avalonia.Media.Imaging;
using SkiaSharp;

namespace Pica.Viewer.Services;

internal sealed class AvaloniaBitmapDecoder : IImageDecoder
{
    public PixelSize ReadPixelSize(Stream sourceStream, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sourceStream);
        ct.ThrowIfCancellationRequested();
        using SKManagedStream managedStream = new(sourceStream);
        using SKCodec codec = SKCodec.Create(managedStream)
            ?? throw new InvalidDataException("Failed to read the image dimensions.");
        SKImageInfo imageInfo = codec.Info;
        bool swapDimensions = codec.EncodedOrigin is SKEncodedOrigin.LeftTop
            or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom
            or SKEncodedOrigin.LeftBottom;
        ct.ThrowIfCancellationRequested();

        return swapDimensions
            ? new PixelSize(imageInfo.Height, imageInfo.Width)
            : new PixelSize(imageInfo.Width, imageInfo.Height);
    }

    public Bitmap Decode(Stream sourceStream, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sourceStream);

        return DecodeBitmap(() => new Bitmap(sourceStream), ct);
    }

    public Bitmap DecodeToWidth(Stream sourceStream, int width, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sourceStream);

        return DecodeBitmap(
            () => Bitmap.DecodeToWidth(
                sourceStream,
                width,
                BitmapInterpolationMode.MediumQuality),
            ct);
    }

    private static Bitmap DecodeBitmap(Func<Bitmap> decode, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Bitmap bitmap = decode();

        if (ct.IsCancellationRequested)
        {
            bitmap.Dispose();
            ct.ThrowIfCancellationRequested();
        }

        return bitmap;
    }
}
