using Avalonia.Media.Imaging;

namespace Pica.Viewer.Services;

internal sealed class ApngImageFrameDecoder :
    IImageFrameDecoder
{
    public ImageFrameDecoderKind Kind =>
        ImageFrameDecoderKind.ApngAnimation;

    public DecodedImage Decode(
        Stream sourceStream,
        ImageDecoderSelection decoderSelection,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sourceStream);
        ArgumentNullException.ThrowIfNull(decoderSelection);
        ct.ThrowIfCancellationRequested();
        long initialPosition = sourceStream.Position;

        if (!ApngParser.IsAnimated(
            sourceStream,
            ct))
        {
            sourceStream.Position = initialPosition;
            Bitmap bitmap = decoderSelection.Decoder.Decode(
                sourceStream,
                ct);

            return DecodedImage.CreateSingle(bitmap);
        }

        sourceStream.Position = initialPosition;
        ApngAnimationData animation =
            ApngParser.Parse(
                sourceStream,
                ct);
        ApngProgressiveImageFrameReader frameReader =
            new(animation);

        if (frameReader.FrameCount == 1)
        {
            try
            {
                DecodedImageFrame frame =
                    frameReader.ReadFrame(
                        0,
                        ct);

                return DecodedImage.CreateSingle(
                    frame.Bitmap);
            }
            finally
            {
                frameReader.Dispose();
            }
        }

        return ProgressiveImageDecoder.Decode(
            frameReader,
            decoderSelection.FramePresentationMode,
            decoderSelection.EffectiveAnimationBufferingPolicy,
            ct);
    }
}
