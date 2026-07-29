namespace Pica.Viewer.Services;

internal sealed class ViewerAnimationFrameRequestedEventArgs : EventArgs
{
    internal Action<TimeSpan> FrameAction { get; }

    internal ViewerAnimationFrameRequestedEventArgs(
        Action<TimeSpan> frameAction)
    {
        FrameAction = frameAction
            ?? throw new ArgumentNullException(nameof(frameAction));
    }
}
