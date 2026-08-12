using ImageMagick;

namespace Pica.Viewer.Services;

internal sealed class FullResolutionImageLoader :
    IFullResolutionImageLoader
{
    private static readonly SemaphoreSlim DecodeLock = new(1, 1);

    private readonly IImageDecoderResolver _decoderResolver;
    private readonly IMultiFrameImageDecoder _multiFrameImageDecoder;

    public FullResolutionImageLoader(
        IImageDecoderResolver decoderResolver,
        IMultiFrameImageDecoder multiFrameImageDecoder)
    {
        _decoderResolver = decoderResolver ?? throw new ArgumentNullException(nameof(decoderResolver));
        _multiFrameImageDecoder = multiFrameImageDecoder
            ?? throw new ArgumentNullException(nameof(multiFrameImageDecoder));
    }

    public async Task<DecodedImageContent> LoadAsync(
        string fullPath,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);
        ImageDecoderSelection decoderSelection =
            _decoderResolver.Resolve(fullPath);
        byte[] data = await File.ReadAllBytesAsync(
            fullPath,
            ct).ConfigureAwait(false);

        if (!IsoBmffMixedContentSupport.IsSupportedFile(fullPath))
        {
            DecodedImage image = await DecodeBytesAsync(
                data,
                decoderSelection,
                ct).ConfigureAwait(false);

            return DecodedImageContent.CreateSingle(image);
        }

        IsoBmffTopLevelContent? topLevelContent =
            IsoBmffTopLevelContentReader.Read(data);
        IReadOnlyList<IsoBmffAnimationTrack> animationTracks =
            IsoBmffAnimationMetadataReader.ReadAll(data);

        if (animationTracks.Count == 0)
        {
            DecodedImage image = await DecodeBytesAsync(
                data,
                decoderSelection,
                ct).ConfigureAwait(false);

            return DecodedImageContent.CreateSingle(image);
        }

        int stillImageCount = topLevelContent is null
            ? 0
            : await ReadStillImageCountAsync(
                data,
                decoderSelection,
                ct).ConfigureAwait(false);

        if ((stillImageCount == 0)
            && (animationTracks.Count == 1))
        {
            DecodedImage image = await DecodeBytesAsync(
                data,
                decoderSelection,
                ct).ConfigureAwait(false);

            return DecodedImageContent.CreateSingle(image);
        }

        bool startsWithStillImages =
            (stillImageCount > 0)
            && (topLevelContent is not null)
            && topLevelContent.StartsWithStillImages;
        int initialGroupIndex = startsWithStillImages
            ? 0
            : stillImageCount > 0
                ? 1
                : 0;
        List<DecodedImageContentGroup> groups = [];

        if (stillImageCount > 0)
        {
            ImageDecoderSelection stillImageSelection =
                CreateStillImageSelection(decoderSelection);
            ImageContentGroupDefinition stillDefinition = new(
                ImageContentGroupKind.StillImages,
                stillImageCount,
                stillImageSelection.FrameNumbering);

            if (startsWithStillImages)
            {
                DecodedImage stillImage =
                    await DecodeStillImagesAsync(
                        data,
                        stillImageSelection,
                        ct).ConfigureAwait(false);
                groups.Add(new DecodedImageContentGroup(
                    stillDefinition,
                    stillImage));
            }
            else
            {
                groups.Add(new DecodedImageContentGroup(
                    stillDefinition,
                    cancellationToken => DecodeStillImagesAsync(
                        data,
                        stillImageSelection,
                        cancellationToken)));
            }
        }

        for (int trackIndex = 0;
            trackIndex < animationTracks.Count;
            trackIndex++)
        {
            IsoBmffAnimationTrack animationTrack =
                animationTracks[trackIndex];
            ImageDecoderSelection animationSelection =
                CreateAnimationSelection(
                    decoderSelection,
                    trackIndex);
            ImageContentGroupDefinition animationDefinition = new(
                ImageContentGroupKind.Animation,
                animationTrack.Metadata.FrameDurations.Count);
            int groupIndex = groups.Count;

            if (groupIndex == initialGroupIndex)
            {
                DecodedImage animationImage =
                    await DecodeBytesAsync(
                        data,
                        animationSelection,
                        ct).ConfigureAwait(false);
                groups.Add(new DecodedImageContentGroup(
                    animationDefinition,
                    animationImage));
            }
            else
            {
                groups.Add(new DecodedImageContentGroup(
                    animationDefinition,
                    cancellationToken => DecodeBytesAsync(
                        data,
                        animationSelection,
                        cancellationToken)));
            }
        }

        return new DecodedImageContent(
            groups.AsReadOnly(),
            initialGroupIndex);
    }

    private static ImageDecoderSelection CreateAnimationSelection(
        ImageDecoderSelection decoderSelection,
        int trackIndex)
    {
        return decoderSelection with
        {
            AnimationTrackSelection =
                IsoBmffAnimationTrackSelection.Create(
                    trackIndex)
        };
    }

    private static ImageDecoderSelection CreateStillImageSelection(
        ImageDecoderSelection decoderSelection)
    {
        return decoderSelection with
        {
            FramePresentationMode =
                ImageFramePresentationModes.ManualNavigation,
            FrameDecoderKind = ImageFrameDecoderKind.Magick,
            InitialFrameSelection = ImageInitialFrameSelection.First,
            FrameNumbering = ImageFrameNumbering.Forward,
            AnimationTrackSelection = null
        };
    }

    private async Task<DecodedImage> DecodeBytesAsync(
        byte[] data,
        ImageDecoderSelection decoderSelection,
        CancellationToken ct)
    {
        return await RunDecodeLockedAsync(
                () =>
                {
                    using MemoryStream stream = new(
                        data,
                        0,
                        data.Length,
                        writable: false,
                        publiclyVisible: true);

                    return _multiFrameImageDecoder.Decode(
                        stream,
                        decoderSelection,
                        ct);
                },
                ct).ConfigureAwait(false);
    }

    private async Task<DecodedImage> DecodeStillImagesAsync(
        byte[] data,
        ImageDecoderSelection decoderSelection,
        CancellationToken ct)
    {
        return await RunDecodeLockedAsync(
                () =>
                {
                    using IsoBmffMovieBoxProjection projection =
                        new(data);
                    using MemoryStream stream = new(
                        projection.Data,
                        writable: false);

                    return _multiFrameImageDecoder.Decode(
                        stream,
                        decoderSelection,
                        ct);
                },
                ct).ConfigureAwait(false);
    }

    private async Task<int> ReadStillImageCountAsync(
        byte[] data,
        ImageDecoderSelection decoderSelection,
        CancellationToken ct)
    {
        return await RunDecodeLockedAsync(
                () =>
                {
                    using IsoBmffMovieBoxProjection projection =
                        new(data);
                    using MemoryStream stream = new(
                        projection.Data,
                        writable: false);
                    using MagickImageCollection images = new();
                    MagickReadSettings? readSettings =
                        decoderSelection.MultiFrameReadFormat
                            is MagickFormat format
                                ? new MagickReadSettings
                                {
                                    Format = format
                                }
                                : null;
                    try
                    {
                        MagickImageCollectionReader.Ping(
                            images,
                            stream,
                            readSettings);
                    }
                    catch (MagickException)
                    {
                        return 0;
                    }

                    ct.ThrowIfCancellationRequested();

                    return images.Count;
                },
                ct).ConfigureAwait(false);
    }

    private static async Task<TResult> RunDecodeLockedAsync<TResult>(
        Func<TResult> operation,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(operation);
        await DecodeLock.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            return await Task.Run(
                operation,
                ct).ConfigureAwait(false);
        }
        finally
        {
            DecodeLock.Release();
        }
    }
}
