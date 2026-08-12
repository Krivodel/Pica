using Avalonia.Media.Imaging;
using ImageMagick;

namespace Pica.Viewer.Services;

internal sealed class MagickAnimatedImageFrameDecoder :
    IImageFrameDecoder
{
    public ImageFrameDecoderKind Kind =>
        ImageFrameDecoderKind.MagickAnimation;

    private readonly MagickMultiFrameImageDecoder _multiFrameImageDecoder;

    public MagickAnimatedImageFrameDecoder(
        MagickMultiFrameImageDecoder multiFrameImageDecoder)
    {
        _multiFrameImageDecoder = multiFrameImageDecoder
            ?? throw new ArgumentNullException(
                nameof(multiFrameImageDecoder));
    }

    public DecodedImage Decode(
        Stream sourceStream,
        ImageDecoderSelection decoderSelection,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sourceStream);
        ArgumentNullException.ThrowIfNull(decoderSelection);
        ct.ThrowIfCancellationRequested();
        MemoryStream bufferedStream =
            ImageStreamBuffer.CopyToMemory(
                sourceStream,
                ct);

        try
        {
            IReadOnlyList<IsoBmffAnimationTrack> sequenceTracks =
                IsoBmffAnimationMetadataReader.ReadAll(
                    bufferedStream);

            return sequenceTracks.Count == 0
                ? DecodeWithoutSequenceMetadata(
                    bufferedStream,
                    decoderSelection,
                    ct)
                : DecodeSelectedSequence(
                    bufferedStream,
                    sequenceTracks,
                    decoderSelection,
                    ct);
        }
        catch
        {
            bufferedStream.Dispose();
            throw;
        }
    }

    private DecodedImage DecodeWithoutSequenceMetadata(
        MemoryStream bufferedStream,
        ImageDecoderSelection decoderSelection,
        CancellationToken ct)
    {
        using MagickImageCollection metadata = new();
        MagickReadSettings? readSettings =
            CreateReadSettings(
                decoderSelection.MultiFrameReadFormat);
        bufferedStream.Position = 0;
        MagickImageCollectionReader.Ping(
            metadata,
            bufferedStream,
            readSettings);
        ct.ThrowIfCancellationRequested();
        bufferedStream.Position = 0;

        if (metadata.Count <= 1)
        {
            using (bufferedStream)
            {
                Bitmap bitmap =
                    decoderSelection.Decoder.Decode(
                        bufferedStream,
                        ct);

                return DecodedImage.CreateSingle(bitmap);
            }
        }

        ImageFramePresentationModes effectiveMode =
            MagickAnimationMetadata.GetEffectivePresentationMode(
                metadata,
                decoderSelection.FramePresentationMode);

        if (!effectiveMode.HasFlag(
            ImageFramePresentationModes.AutomaticPlayback))
        {
            using (bufferedStream)
            {
                return _multiFrameImageDecoder.Decode(
                    bufferedStream,
                    decoderSelection,
                    ct);
            }
        }

        List<TimeSpan> frameDurations = metadata
            .Select(MagickAnimationMetadata.GetFrameDuration)
            .ToList();
        uint animationIterations =
            metadata[0].AnimationIterations;

        return DecodeProgressively(
            bufferedStream,
            frameDurations.AsReadOnly(),
            animationIterations,
            effectiveMode,
            decoderSelection,
            ct);
    }

    private static DecodedImage DecodeSelectedSequence(
        MemoryStream bufferedStream,
        IReadOnlyList<IsoBmffAnimationTrack> sequenceTracks,
        ImageDecoderSelection decoderSelection,
        CancellationToken ct)
    {
        int selectedTrackIndex = decoderSelection
            .AnimationTrackSelection?.TrackIndex
            ?? 0;

        if ((selectedTrackIndex < 0)
            || (selectedTrackIndex >= sequenceTracks.Count))
        {
            bufferedStream.Dispose();
            throw new InvalidDataException(
                $"The requested animation track index {selectedTrackIndex} is not present in the ISO BMFF container.");
        }

        IsoBmffAnimationMetadata sequenceMetadata =
            sequenceTracks[selectedTrackIndex].Metadata;

        if (sequenceTracks.Count > 1)
        {
            byte[] projectedData =
                IsoBmffAnimationTrackProjection.Create(
                    bufferedStream,
                    selectedTrackIndex);
            bufferedStream.Dispose();
            bufferedStream = new MemoryStream(
                projectedData,
                0,
                projectedData.Length,
                writable: false,
                publiclyVisible: true);
        }

        return DecodeProgressively(
            bufferedStream,
            sequenceMetadata.FrameDurations,
            sequenceMetadata.AnimationIterations,
            decoderSelection.FramePresentationMode,
            decoderSelection,
            ct);
    }

    private static DecodedImage DecodeProgressively(
        MemoryStream bufferedStream,
        IReadOnlyList<TimeSpan> frameDurations,
        uint animationIterations,
        ImageFramePresentationModes framePresentationMode,
        ImageDecoderSelection decoderSelection,
        CancellationToken ct)
    {
        MagickProgressiveImageFrameReader frameReader = new(
            bufferedStream,
            frameDurations,
            animationIterations,
            decoderSelection.MultiFrameReadFormat,
            decoderSelection
                .EffectiveAnimationBufferingPolicy,
            decoderSelection
                .EffectiveMagickAnimationDecodingPolicy);

        return ProgressiveImageDecoder.Decode(
            frameReader,
            framePresentationMode,
            decoderSelection.EffectiveAnimationBufferingPolicy,
            ct);
    }

    private static MagickReadSettings? CreateReadSettings(
        MagickFormat? readFormat)
    {
        return readFormat is MagickFormat format
            ? new MagickReadSettings
            {
                Format = format
            }
            : null;
    }
}
