using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.TestDoubles;

internal sealed class ControlledImageAnimationDelayScheduler :
    IImageAnimationDelayScheduler
{
    internal IReadOnlyList<TimeSpan> RequestedDurations =>
        _requestedDurations;

    private readonly object _sync = new();
    private readonly List<TimeSpan> _requestedDurations = [];
    private readonly Queue<TaskCompletionSource> _completions = [];
    private readonly SemaphoreSlim _requestSignal = new(0);

    public async Task DelayAsync(
        TimeSpan duration,
        CancellationToken ct)
    {
        TaskCompletionSource completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        lock (_sync)
        {
            _requestedDurations.Add(duration);
            _completions.Enqueue(completion);
        }

        _requestSignal.Release();
        using CancellationTokenRegistration registration =
            ct.Register(() => completion.TrySetCanceled(ct));
        await completion.Task.ConfigureAwait(false);
    }

    internal void CompleteNext()
    {
        while (true)
        {
            TaskCompletionSource completion;

            lock (_sync)
            {
                completion = _completions.Dequeue();
            }

            if (completion.TrySetResult())
            {
                return;
            }
        }
    }

    internal async Task WaitForRequestAsync(CancellationToken ct)
    {
        await _requestSignal.WaitAsync(ct).ConfigureAwait(false);
    }
}
