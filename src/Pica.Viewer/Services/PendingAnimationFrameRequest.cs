namespace Pica.Viewer.Services;

internal sealed class PendingAnimationFrameRequest
{
    internal Action<TimeSpan> FrameAction { get; }

    private int _submissionVersion;
    private bool _isCompleted;

    internal PendingAnimationFrameRequest(Action<TimeSpan> frameAction)
    {
        FrameAction = frameAction
            ?? throw new ArgumentNullException(nameof(frameAction));
    }

    internal int BeginSubmission()
    {
        return ++_submissionVersion;
    }

    internal void InvalidateSubmission()
    {
        _submissionVersion++;
    }

    internal void Cancel()
    {
        _isCompleted = true;
        _submissionVersion++;
    }

    internal bool TryComplete(int submissionVersion)
    {
        if (_isCompleted || (submissionVersion != _submissionVersion))
        {
            return false;
        }

        _isCompleted = true;

        return true;
    }
}
