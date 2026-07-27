namespace Pica.Viewer.Services;

public sealed class ViewerAnimationFrameScheduler
{
    public bool HasPendingFrames => _pendingRequests.Count > 0;

    private readonly Action<Action<TimeSpan>> _requestAnimationFrame;
    private readonly List<PendingFrameRequest> _pendingRequests = [];
    private bool _isPresented;

    public ViewerAnimationFrameScheduler(
        Action<Action<TimeSpan>> requestAnimationFrame)
    {
        _requestAnimationFrame = requestAnimationFrame
            ?? throw new ArgumentNullException(nameof(requestAnimationFrame));
    }

    public void RequestAnimationFrame(Action<TimeSpan> frameAction)
    {
        ArgumentNullException.ThrowIfNull(frameAction);

        PendingFrameRequest request = new(frameAction);
        _pendingRequests.Add(request);

        if (_isPresented)
        {
            Submit(request);
        }
    }

    public void SetPresentation(bool isPresented)
    {
        if (_isPresented == isPresented)
        {
            return;
        }

        _isPresented = isPresented;

        if (!_isPresented)
        {
            foreach (PendingFrameRequest request in _pendingRequests)
            {
                request.InvalidateSubmission();
            }

            return;
        }

        foreach (PendingFrameRequest request in _pendingRequests)
        {
            Submit(request);
        }
    }

    public void CancelPendingFrames()
    {
        foreach (PendingFrameRequest request in _pendingRequests)
        {
            request.Cancel();
        }

        _pendingRequests.Clear();
    }

    private void Submit(PendingFrameRequest request)
    {
        int submissionVersion = request.BeginSubmission();
        _requestAnimationFrame(
            frameTime => Complete(request, submissionVersion, frameTime));
    }

    private void Complete(
        PendingFrameRequest request,
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

    private sealed class PendingFrameRequest
    {
        public Action<TimeSpan> FrameAction { get; }

        private int _submissionVersion;
        private bool _isCompleted;

        public PendingFrameRequest(Action<TimeSpan> frameAction)
        {
            FrameAction = frameAction
                ?? throw new ArgumentNullException(nameof(frameAction));
        }

        public int BeginSubmission()
        {
            return ++_submissionVersion;
        }

        public void InvalidateSubmission()
        {
            _submissionVersion++;
        }

        public void Cancel()
        {
            _isCompleted = true;
            _submissionVersion++;
        }

        public bool TryComplete(int submissionVersion)
        {
            if (_isCompleted || submissionVersion != _submissionVersion)
            {
                return false;
            }

            _isCompleted = true;

            return true;
        }
    }
}
