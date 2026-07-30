namespace Pica.Viewer.Services;

internal sealed class AnimationFrameSchedulerCore
{
    internal bool HasPendingFrames => _pendingRequests.Count > 0;

    private readonly Func<Action<TimeSpan>, bool> _trySubmitFrame;
    private readonly List<PendingAnimationFrameRequest> _pendingRequests = [];
    private bool _isPresented;

    internal AnimationFrameSchedulerCore(
        Func<Action<TimeSpan>, bool> trySubmitFrame)
    {
        _trySubmitFrame = trySubmitFrame
            ?? throw new ArgumentNullException(nameof(trySubmitFrame));
    }

    internal void RequestAnimationFrame(Action<TimeSpan> frameAction)
    {
        ArgumentNullException.ThrowIfNull(frameAction);

        PendingAnimationFrameRequest request = new(frameAction);
        _pendingRequests.Add(request);

        if (_isPresented)
        {
            Submit(request);
        }
    }

    internal void SetPresentation(bool isPresented)
    {
        if (_isPresented == isPresented)
        {
            return;
        }

        _isPresented = isPresented;

        if (!_isPresented)
        {
            foreach (PendingAnimationFrameRequest request
                     in _pendingRequests.ToList())
            {
                request.InvalidateSubmission();
            }

            return;
        }

        foreach (PendingAnimationFrameRequest request
                 in _pendingRequests.ToList())
        {
            Submit(request);
        }
    }

    internal void CancelPendingFrames()
    {
        foreach (PendingAnimationFrameRequest request in _pendingRequests)
        {
            request.Cancel();
        }

        _pendingRequests.Clear();
    }

    private void Submit(PendingAnimationFrameRequest request)
    {
        int submissionVersion = request.BeginSubmission();
        bool wasSubmitted = _trySubmitFrame(
            frameTime => Complete(
                request,
                submissionVersion,
                frameTime));

        if (!wasSubmitted)
        {
            request.InvalidateSubmission();
        }
    }

    private void Complete(
        PendingAnimationFrameRequest request,
        int submissionVersion,
        TimeSpan frameTime)
    {
        if (!request.TryComplete(submissionVersion))
        {
            return;
        }

        _pendingRequests.Remove(request);
        request.FrameAction(frameTime);
    }
}
