namespace Pica.Viewer.Services;

internal sealed class ImageContentNavigationRequestedEventArgs : EventArgs
{
    internal int GroupIndex { get; }
    internal int FrameIndex { get; }

    internal ImageContentNavigationRequestedEventArgs(
        int groupIndex,
        int frameIndex)
    {
        GroupIndex = groupIndex;
        FrameIndex = frameIndex;
    }
}
