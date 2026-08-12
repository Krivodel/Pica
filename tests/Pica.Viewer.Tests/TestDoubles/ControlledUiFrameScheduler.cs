using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.TestDoubles;

internal sealed class ControlledUiFrameScheduler : IUiFrameScheduler
{
    internal int PendingFrameCount
    {
        get
        {
            lock (_sync)
            {
                return _pendingFrames.Count;
            }
        }
    }

    private readonly object _sync = new();
    private readonly Queue<Action<TimeSpan>> _pendingFrames = [];

    public void RequestAnimationFrame(Action<TimeSpan> frameAction)
    {
        ArgumentNullException.ThrowIfNull(frameAction);

        lock (_sync)
        {
            _pendingFrames.Enqueue(frameAction);
        }
    }

    internal void RunNext(TimeSpan frameTime)
    {
        Action<TimeSpan> frameAction;

        lock (_sync)
        {
            frameAction = _pendingFrames.Dequeue();
        }

        frameAction(frameTime);
    }
}
