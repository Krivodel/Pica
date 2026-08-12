using ImageMagick;

namespace Pica.Viewer.Services;

internal sealed record ImageDecoderSelection(
    IImageDecoder Decoder,
    ImageFramePresentationModes FramePresentationMode,
    ImageFrameDecoderKind FrameDecoderKind =
        ImageFrameDecoderKind.Magick,
    MagickFormat? MultiFrameReadFormat = null,
    AnimatedImageContainerFormat? AnimatedImageFormat = null,
    ImageInitialFrameSelection InitialFrameSelection =
        ImageInitialFrameSelection.First,
    ImageFrameNumbering FrameNumbering =
        ImageFrameNumbering.Forward,
    ImageAnimationBufferingPolicy? AnimationBufferingPolicy =
        null,
    MagickAnimationDecodingPolicy? MagickDecodingPolicy =
        null,
    IsoBmffAnimationTrackSelection? AnimationTrackSelection =
        null)
{
    internal ImageAnimationBufferingPolicy EffectiveAnimationBufferingPolicy =>
        AnimationBufferingPolicy
        ?? ImageAnimationBufferingPolicy.Lightweight;
    internal MagickAnimationDecodingPolicy
        EffectiveMagickAnimationDecodingPolicy =>
            MagickDecodingPolicy
            ?? MagickAnimationDecodingPolicy
                .DependentFrameSequence;
}
