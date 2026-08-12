using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.TestDoubles;

internal static class MultiFrameImageDecoderTestFactory
{
    internal static IMultiFrameImageDecoder Create()
    {
        MagickMultiFrameImageDecoder magickDecoder = new();
        IImageFrameDecoder[] frameDecoders =
        [
            magickDecoder,
            new ApngImageFrameDecoder(),
            new AnimatedImageFrameDecoder(),
            new SkiaAnimatedImageFrameDecoder(),
            new MagickAnimatedImageFrameDecoder(
                magickDecoder)
        ];

        return new MultiFrameImageDecoder(frameDecoders);
    }
}
