using Avalonia;
using Avalonia.Media.Imaging;
using ImageMagick;

namespace Pica.Viewer.Services;

internal sealed class MagickMultiFrameImageDecoder :
    IImageFrameDecoder
{
    public ImageFrameDecoderKind Kind =>
        ImageFrameDecoderKind.Magick;

    public DecodedImage Decode(
        Stream sourceStream,
        ImageDecoderSelection decoderSelection,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sourceStream);
        ArgumentNullException.ThrowIfNull(decoderSelection);
        ct.ThrowIfCancellationRequested();

        long initialPosition = sourceStream.CanSeek
            ? sourceStream.Position
            : 0;
        using MagickImageCollection images = new();
        MagickReadSettings? readSettings =
            decoderSelection.MultiFrameReadFormat is MagickFormat format
                ? new MagickReadSettings
                {
                    Format = format
                }
                : null;

        if (sourceStream.CanSeek)
        {
            MagickImageCollectionReader.Ping(
                images,
                sourceStream,
                readSettings);
            ct.ThrowIfCancellationRequested();

            if (images.Count <= 1)
            {
                sourceStream.Position = initialPosition;
                Bitmap bitmap = decoderSelection.Decoder.Decode(
                    sourceStream,
                    ct);

                return DecodedImage.CreateSingle(bitmap);
            }

            images.Clear();
            sourceStream.Position = initialPosition;
        }

        MagickImageCollectionReader.Read(
            images,
            sourceStream,
            readSettings);
        ct.ThrowIfCancellationRequested();

        if (images.Count <= 1)
        {
            return CreateDecodedImage(
                images,
                ImageFramePresentationModes.None,
                decoderSelection.InitialFrameSelection,
                decoderSelection.FrameNumbering,
                ct);
        }

        return CreateDecodedImage(
            images,
            decoderSelection.FramePresentationMode,
            decoderSelection.InitialFrameSelection,
            decoderSelection.FrameNumbering,
            ct);
    }

    private static DecodedImage CreateDecodedImage(
        MagickImageCollection images,
        ImageFramePresentationModes framePresentationMode,
        ImageInitialFrameSelection initialFrameSelection,
        ImageFrameNumbering frameNumbering,
        CancellationToken ct)
    {
        uint animationIterations = images[0].AnimationIterations;
        List<TimeSpan> frameDurations = images
            .Select(MagickAnimationMetadata.GetFrameDuration)
            .ToList();
        ImageFramePresentationModes effectiveMode =
            MagickAnimationMetadata.GetEffectivePresentationMode(
                images,
                framePresentationMode);

        if (effectiveMode.HasFlag(
            ImageFramePresentationModes.AutomaticPlayback))
        {
            images.Coalesce();
            ct.ThrowIfCancellationRequested();
        }

        List<DecodedImageFrame> frames = [];

        try
        {
            for (int i = 0; i < images.Count; i++)
            {
                IMagickImage<byte> image = images[i];
                image.AutoOrient();
                ct.ThrowIfCancellationRequested();

                Bitmap bitmap = MagickImageDecoder.CreateBitmap(
                    image,
                    ct);
                frames.Add(new DecodedImageFrame(
                    bitmap,
                    frameDurations[i]));
            }

            return new DecodedImage(
                frames,
                effectiveMode,
                animationIterations,
                GetPreferredInitialFrameIndex(
                    frames,
                    initialFrameSelection),
                frameNumbering);
        }
        catch
        {
            foreach (DecodedImageFrame frame in frames)
            {
                frame.Bitmap.Dispose();
            }

            throw;
        }
    }

    private static int GetPreferredInitialFrameIndex(
        IReadOnlyList<DecodedImageFrame> frames,
        ImageInitialFrameSelection initialFrameSelection)
    {
        return ImageInitialFrameSelector.GetPreferredFrameIndex(
            frames.Count,
            initialFrameSelection,
            frameIndex =>
            {
                PixelSize pixelSize =
                    frames[frameIndex].Bitmap.PixelSize;

                return checked(
                    (ulong)pixelSize.Width
                    * (ulong)pixelSize.Height);
            });
    }
}
