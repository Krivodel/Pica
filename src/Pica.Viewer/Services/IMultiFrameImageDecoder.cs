namespace Pica.Viewer.Services;

internal interface IMultiFrameImageDecoder
{
    DecodedImage Decode(
        Stream sourceStream,
        ImageDecoderSelection decoderSelection,
        CancellationToken ct);
}
