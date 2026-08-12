using ImageMagick;

namespace Pica.Viewer.Services;

public sealed class ImageFormatRegistry : IImageFormatRegistry, IImageDecoderResolver
{
    private static readonly IImageDecoder DefaultDecoder = new AvaloniaBitmapDecoder();
    private static readonly IImageDecoder MagickDecoder = new MagickImageDecoder();
    private static readonly IImageDecoder IcoDecoder = new IcoImageDecoder();
    private static readonly IReadOnlyDictionary<string, ImageFormatDefinition> FormatsByExtension =
        new Dictionary<string, ImageFormatDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            [PicaImageFormats.PngExtension] = new(
                PicaImageFormats.PngContentType,
                DefaultDecoder,
                ImageFramePresentationModes.AutomaticPlayback,
                ImageFrameDecoderKind.ApngAnimation,
                AnimationBufferingPolicy:
                    ImageAnimationBufferingPolicy.Lightweight),
            [".apng"] = new(
                PicaImageFormats.PngContentType,
                DefaultDecoder,
                ImageFramePresentationModes.AutomaticPlayback,
                ImageFrameDecoderKind.ApngAnimation,
                AnimationBufferingPolicy:
                    ImageAnimationBufferingPolicy.Lightweight),
            [".jpg"] = new(
                PicaImageFormats.JpegContentType,
                DefaultDecoder,
                ImageFramePresentationModes.None),
            [".jpeg"] = new(
                PicaImageFormats.JpegContentType,
                DefaultDecoder,
                ImageFramePresentationModes.None),
            [".webp"] = new(
                "image/webp",
                DefaultDecoder,
                ImageFramePresentationModes.AutomaticPlayback,
                ImageFrameDecoderKind.SkiaAnimation,
                AnimationBufferingPolicy:
                    ImageAnimationBufferingPolicy.WebP),
            [".bmp"] = new(
                "image/bmp",
                DefaultDecoder,
                ImageFramePresentationModes.None),
            [".gif"] = new(
                "image/gif",
                DefaultDecoder,
                ImageFramePresentationModes.AutomaticPlayback,
                ImageFrameDecoderKind.AnimatedImage,
                AnimatedImageFormat:
                    AnimatedImageContainerFormat.Gif,
                AnimationBufferingPolicy:
                    ImageAnimationBufferingPolicy.Lightweight),
            [".ico"] = new(
                "image/x-icon",
                IcoDecoder,
                ImageFramePresentationModes.ManualNavigation,
                ImageFrameDecoderKind.Magick,
                MagickFormat.Ico,
                InitialFrameSelection:
                    ImageInitialFrameSelection.LargestArea,
                FrameNumbering:
                    ImageFrameNumbering.Reverse),
            [".cur"] = new(
                "image/x-icon",
                IcoDecoder,
                ImageFramePresentationModes.ManualNavigation,
                ImageFrameDecoderKind.Magick,
                MagickFormat.Ico,
                InitialFrameSelection:
                    ImageInitialFrameSelection.LargestArea,
                FrameNumbering:
                    ImageFrameNumbering.Reverse),
            [PicaImageFormats.AvifExtension] = new(
                PicaImageFormats.AvifContentType,
                MagickDecoder,
                ImageFramePresentationModes.ManualNavigation
                    | ImageFramePresentationModes.AutomaticPlayback,
                ImageFrameDecoderKind.MagickAnimation,
                AnimationBufferingPolicy:
                    ImageAnimationBufferingPolicy.HighEfficiency,
                MagickDecodingPolicy:
                    MagickAnimationDecodingPolicy
                        .DependentFrameSequence),
            [PicaImageFormats.HeicExtension] = new(
                PicaImageFormats.HeicContentType,
                MagickDecoder,
                ImageFramePresentationModes.ManualNavigation
                    | ImageFramePresentationModes.AutomaticPlayback,
                ImageFrameDecoderKind.MagickAnimation,
                AnimationBufferingPolicy:
                    ImageAnimationBufferingPolicy.HighEfficiency,
                MagickDecodingPolicy:
                    MagickAnimationDecodingPolicy
                        .DependentFrameSequence),
            [PicaImageFormats.HeifExtension] = new(
                PicaImageFormats.HeifContentType,
                MagickDecoder,
                ImageFramePresentationModes.ManualNavigation
                    | ImageFramePresentationModes.AutomaticPlayback,
                ImageFrameDecoderKind.MagickAnimation,
                AnimationBufferingPolicy:
                    ImageAnimationBufferingPolicy.HighEfficiency,
                MagickDecodingPolicy:
                    MagickAnimationDecodingPolicy
                        .DependentFrameSequence),
            [PicaImageFormats.TifExtension] = new(
                PicaImageFormats.TiffContentType,
                MagickDecoder,
                ImageFramePresentationModes.ManualNavigation),
            [PicaImageFormats.TiffExtension] = new(
                PicaImageFormats.TiffContentType,
                MagickDecoder,
                ImageFramePresentationModes.ManualNavigation)
        };

    public bool IsSupportedFileName(string fileName)
    {
        string extension = Path.GetExtension(fileName);

        return FormatsByExtension.ContainsKey(extension);
    }

    public string GetContentType(string fileName)
    {
        string extension = Path.GetExtension(fileName);

        ImageFormatDefinition? format = FormatsByExtension.GetValueOrDefault(extension);

        return format?.ContentType ?? PicaImageFormats.PngContentType;
    }

    public string GetExtension(string fileName)
    {
        string extension = Path.GetExtension(fileName);

        return string.IsNullOrWhiteSpace(extension) ? PicaImageFormats.PngExtension : extension;
    }

    ImageDecoderSelection IImageDecoderResolver.Resolve(
        string fileName)
    {
        string extension = Path.GetExtension(fileName);
        ImageFormatDefinition? format = FormatsByExtension.GetValueOrDefault(extension);

        return format is null
            ? new ImageDecoderSelection(
                DefaultDecoder,
                ImageFramePresentationModes.None)
            : new ImageDecoderSelection(
                format.Decoder,
                format.FramePresentationMode,
                format.FrameDecoderKind,
                format.MultiFrameReadFormat,
                format.AnimatedImageFormat,
                format.InitialFrameSelection,
                format.FrameNumbering,
                format.AnimationBufferingPolicy,
                format.MagickDecodingPolicy);
    }

    private sealed record ImageFormatDefinition(
        string ContentType,
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
            null);
}
