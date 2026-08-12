namespace Pica.Viewer.Services;

internal static class ImageInitialFrameSelector
{
    internal static int GetPreferredFrameIndex(
        int frameCount,
        ImageInitialFrameSelection selection,
        Func<int, ulong> getFrameArea)
    {
        if (frameCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(frameCount),
                frameCount,
                "The image must contain at least one frame.");
        }

        ArgumentNullException.ThrowIfNull(getFrameArea);

        if (selection == ImageInitialFrameSelection.First)
        {
            return 0;
        }

        int largestFrameIndex = 0;
        ulong largestArea = 0;

        for (int i = 0; i < frameCount; i++)
        {
            ulong area = getFrameArea(i);

            if (area > largestArea)
            {
                largestArea = area;
                largestFrameIndex = i;
            }
        }

        return largestFrameIndex;
    }
}
