namespace Pica.Viewer.Services;

internal interface IImageFrameDecoder
{
    ImageFrameDecoderKind Kind { get; }

    DecodedImage Decode(
        Stream sourceStream,
        ImageDecoderSelection decoderSelection,
        CancellationToken ct);
}
