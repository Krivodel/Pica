namespace Pica.Viewer.Services;

internal sealed record IsoBmffAnimationTrack(
    int BoxOffset,
    int BoxEnd,
    uint TrackId,
    IsoBmffAnimationMetadata Metadata);
