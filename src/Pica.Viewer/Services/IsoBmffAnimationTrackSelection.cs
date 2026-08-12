namespace Pica.Viewer.Services;

internal sealed record IsoBmffAnimationTrackSelection(int TrackIndex)
{
    internal static IsoBmffAnimationTrackSelection Create(
        int trackIndex)
    {
        if (trackIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(trackIndex),
                trackIndex,
                "The animation track index must not be negative.");
        }

        return new IsoBmffAnimationTrackSelection(trackIndex);
    }
}
