using Avalonia;
using Avalonia.Media.Imaging;
using ImageMagick;

namespace Pica.Viewer.Services;

internal sealed class IcoImageDecoder : IImageDecoder
{
    public PixelSize ReadPixelSize(
        Stream sourceStream,
        CancellationToken ct)
    {
        using MagickImageCollection images =
            ReadCollectionMetadata(sourceStream, ct);
        IMagickImage<byte> image =
            images[GetLargestImageIndex(images)];

        return new PixelSize(
            checked((int)image.Width),
            checked((int)image.Height));
    }

    public bool ReadHasAlpha(
        Stream sourceStream,
        CancellationToken ct)
    {
        using MagickImageCollection images =
            ReadCollection(sourceStream, ct);
        IMagickImage<byte> image =
            images[GetLargestImageIndex(images)];

        return image.HasAlpha;
    }

    public Bitmap Decode(
        Stream sourceStream,
        CancellationToken ct)
    {
        using MagickImageCollection images =
            ReadCollection(sourceStream, ct);
        IMagickImage<byte> image =
            images[GetLargestImageIndex(images)];
        image.AutoOrient();

        return MagickImageDecoder.CreateBitmap(image, ct);
    }

    public Bitmap DecodeToWidth(
        Stream sourceStream,
        int width,
        CancellationToken ct)
    {
        using MagickImageCollection images =
            ReadCollection(sourceStream, ct);
        IMagickImage<byte> image =
            images[GetLargestImageIndex(images)];
        image.AutoOrient();
        MagickImageDecoder.ResizeToWidth(
            image,
            width,
            ct);

        return MagickImageDecoder.CreateBitmap(image, ct);
    }

    private static MagickImageCollection ReadCollectionMetadata(
        Stream sourceStream,
        CancellationToken ct)
    {
        return ReadCollectionCore(
            sourceStream,
            ct,
            Ping);
    }

    private static MagickImageCollection ReadCollection(
        Stream sourceStream,
        CancellationToken ct)
    {
        return ReadCollectionCore(
            sourceStream,
            ct,
            Read);
    }

    private static MagickImageCollection ReadCollectionCore(
        Stream sourceStream,
        CancellationToken ct,
        Action<MagickImageCollection, Stream, MagickReadSettings>
            readOperation)
    {
        ArgumentNullException.ThrowIfNull(sourceStream);
        ArgumentNullException.ThrowIfNull(readOperation);
        ct.ThrowIfCancellationRequested();
        MagickImageCollection images = new();

        try
        {
            readOperation(
                images,
                sourceStream,
                CreateReadSettings());
            ValidateCollection(images);
            ct.ThrowIfCancellationRequested();

            return images;
        }
        catch
        {
            images.Dispose();
            throw;
        }
    }

    private static void Ping(
        MagickImageCollection images,
        Stream sourceStream,
        MagickReadSettings readSettings)
    {
        images.Ping(sourceStream, readSettings);
    }

    private static void Read(
        MagickImageCollection images,
        Stream sourceStream,
        MagickReadSettings readSettings)
    {
        images.Read(sourceStream, readSettings);
    }

    private static MagickReadSettings CreateReadSettings()
    {
        return new MagickReadSettings
        {
            Format = MagickFormat.Ico
        };
    }

    private static int GetLargestImageIndex(
        MagickImageCollection images)
    {
        return ImageInitialFrameSelector.GetPreferredFrameIndex(
            images.Count,
            ImageInitialFrameSelection.LargestArea,
            frameIndex =>
            {
                IMagickImage<byte> image = images[frameIndex];

                return (ulong)image.Width * image.Height;
            });
    }

    private static void ValidateCollection(
        MagickImageCollection images)
    {
        if (images.Count == 0)
        {
            throw new InvalidDataException(
                "The icon does not contain any images.");
        }
    }
}
