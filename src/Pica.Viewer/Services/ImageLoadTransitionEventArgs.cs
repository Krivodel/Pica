using Avalonia;

namespace Pica.Viewer.Services;

internal sealed class ImageLoadTransitionEventArgs : EventArgs
{
    internal ImageLoadTransitionKind Kind { get; }
    internal bool WasPreviewDisplayed { get; }
    internal PixelSize PreviousPixelSize { get; }
    internal PixelSize CurrentPixelSize { get; }

    internal ImageLoadTransitionEventArgs(
        ImageLoadTransitionKind kind,
        bool wasPreviewDisplayed,
        PixelSize previousPixelSize,
        PixelSize currentPixelSize)
    {
        Kind = kind;
        WasPreviewDisplayed = wasPreviewDisplayed;
        PreviousPixelSize = previousPixelSize;
        CurrentPixelSize = currentPixelSize;
    }
}
