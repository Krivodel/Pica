using Avalonia.Media.Imaging;

namespace Pica.Viewer.Services;

internal sealed class SkiaAnimatedImageFrameDecoder :
    IImageFrameDecoder
{
    public ImageFrameDecoderKind Kind =>
        ImageFrameDecoderKind.SkiaAnimation;

    public DecodedImage Decode(
        Stream sourceStream,
        ImageDecoderSelection decoderSelection,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sourceStream);
        ArgumentNullException.ThrowIfNull(decoderSelection);
        ct.ThrowIfCancellationRequested();
        long initialPosition = sourceStream.Position;
        MemoryStream bufferedStream = ImageStreamBuffer.CopyToMemory(
            sourceStream,
            ct);
        SkiaProgressiveImageFrameReader frameReader =
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
}
