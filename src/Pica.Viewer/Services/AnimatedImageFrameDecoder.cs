using Avalonia.Media.Imaging;
using ImageMagick;

namespace Pica.Viewer.Services;

internal sealed class AnimatedImageFrameDecoder :
    IImageFrameDecoder
{
    public ImageFrameDecoderKind Kind =>
        ImageFrameDecoderKind.AnimatedImage;

    public DecodedImage Decode(
        Stream sourceStream,
        ImageDecoderSelection decoderSelection,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sourceStream);
        ArgumentNullException.ThrowIfNull(decoderSelection);
        ct.ThrowIfCancellationRequested();
        AnimatedImageContainerFormat format =
            decoderSelection.AnimatedImageFormat
            ?? throw new InvalidOperationException(
                "An animated image container format was not selected.");
        long initialPosition = sourceStream.Position;

        if (!IsAnimated(
            sourceStream,
            format,
            ct))
        {
            sourceStream.Position = initialPosition;
            Bitmap bitmap = decoderSelection.Decoder.Decode(
                sourceStream,
                ct);

            return DecodedImage.CreateSingle(bitmap);
        }

        sourceStream.Position = initialPosition;
        MemoryStream bufferedStream = ImageStreamBuffer.CopyToMemory(
            sourceStream,
            ct);
        AnimatedImageProgressiveFrameReader frameReader =
            new(bufferedStream);

        if (frameReader.FrameCount <= 1)
        {
            frameReader.Dispose();
            sourceStream.Position = initialPosition;
            Bitmap bitmap = decoderSelection.Decoder.Decode(
                sourceStream,
                ct);

            return DecodedImage.CreateSingle(bitmap);
        }

        return ProgressiveImageDecoder.Decode(
            frameReader,
            decoderSelection.FramePresentationMode,
            decoderSelection.EffectiveAnimationBufferingPolicy,
            ct);
    }

    private static bool IsAnimated(
        Stream sourceStream,
        AnimatedImageContainerFormat format,
        CancellationToken ct)
    {
        return format switch
        {
            AnimatedImageContainerFormat.Gif =>
                HasMultipleFrames(
                    sourceStream,
                    MagickFormat.Gif,
                    ct),
            _ => throw new InvalidOperationException(
                $"Unsupported animated image container format '{format}'.")
        };
    }

    private static bool HasMultipleFrames(
        Stream sourceStream,
        MagickFormat format,
        CancellationToken ct)
    {
        long initialPosition = sourceStream.Position;

        try
        {
            using MagickImageCollection images = new();
            images.Ping(
                sourceStream,
                new MagickReadSettings
                {
                    Format = format
                });
            ct.ThrowIfCancellationRequested();

            return images.Count > 1;
        }
        finally
        {
            sourceStream.Position = initialPosition;
        }
    }

}
