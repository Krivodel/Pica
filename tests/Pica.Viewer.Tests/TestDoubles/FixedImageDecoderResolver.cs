using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.TestDoubles;

internal sealed class FixedImageDecoderResolver : IImageDecoderResolver
{
    private readonly ImageDecoderSelection _selection;

    internal FixedImageDecoderResolver(IImageDecoder decoder)
    {
        ArgumentNullException.ThrowIfNull(decoder);
        _selection = new ImageDecoderSelection(
            decoder,
            ImageFramePresentationModes.None);
    }

    public ImageDecoderSelection Resolve(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        return _selection;
    }
}
