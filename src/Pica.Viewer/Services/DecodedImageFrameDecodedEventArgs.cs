namespace Pica.Viewer.Services;

internal sealed class DecodedImageFrameDecodedEventArgs : EventArgs
{
    internal int FrameIndex { get; }

    internal DecodedImageFrameDecodedEventArgs(int frameIndex)
    {
        FrameIndex = frameIndex;
    }
}
