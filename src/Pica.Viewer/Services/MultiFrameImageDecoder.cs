namespace Pica.Viewer.Services;

internal sealed class MultiFrameImageDecoder :
    IMultiFrameImageDecoder
{
    private readonly IReadOnlyDictionary<
        ImageFrameDecoderKind,
        IImageFrameDecoder> _frameDecoders;

    public MultiFrameImageDecoder(
        IEnumerable<IImageFrameDecoder> frameDecoders)
    {
        ArgumentNullException.ThrowIfNull(frameDecoders);
        _frameDecoders = frameDecoders.ToDictionary(
            frameDecoder => frameDecoder.Kind);
    }

    public DecodedImage Decode(
        Stream sourceStream,
        ImageDecoderSelection decoderSelection,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sourceStream);
        ArgumentNullException.ThrowIfNull(decoderSelection);

        if (sourceStream.CanSeek)
        {
            return DecodeSeekable(
                sourceStream,
                decoderSelection,
                ct);
        }

        using MemoryStream bufferedStream =
            ImageStreamBuffer.CopyToMemory(
                sourceStream,
                ct);

        return DecodeSeekable(
            bufferedStream,
            decoderSelection,
            ct);
    }

    private DecodedImage DecodeSeekable(
        Stream sourceStream,
        ImageDecoderSelection decoderSelection,
        CancellationToken ct)
    {
        if (decoderSelection.FramePresentationMode
            == ImageFramePresentationModes.None)
        {
            return DecodedImage.CreateSingle(
                decoderSelection.Decoder.Decode(
                    sourceStream,
                    ct));
        }

        if (!_frameDecoders.TryGetValue(
            decoderSelection.FrameDecoderKind,
            out IImageFrameDecoder? frameDecoder))
        {
            throw new InvalidOperationException(
                $"No image frame decoder is registered for '{decoderSelection.FrameDecoderKind}'.");
        }

        return frameDecoder.Decode(
            sourceStream,
            decoderSelection,
            ct);
    }
}
