using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.TestDoubles;

internal sealed class ControlledViewerUiDispatcher : IViewerUiDispatcher, IDisposable
{
    private readonly object _sync = new();
    private readonly Queue<Action> _pendingInvocations = [];
    private readonly SemaphoreSlim _pendingSignal = new(0);

    public Task InvokeAsync(
        Action action,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(action);
        ct.ThrowIfCancellationRequested();
        TaskCompletionSource<bool> completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        Enqueue(
            () =>
            {
                try
                {
                    action();
                    completion.TrySetResult(true);
                }
                catch (Exception ex)
                {
                    completion.TrySetException(ex);
                }
            });

        return completion.Task;
    }

    public Task<TResult> InvokeAsync<TResult>(
        Func<TResult> action,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(action);
        ct.ThrowIfCancellationRequested();
        TaskCompletionSource<TResult> completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        Enqueue(
            () =>
            {
                try
                {
                    completion.TrySetResult(action());
                }
                catch (Exception ex)
                {
                    completion.TrySetException(ex);
                }
            });

        return completion.Task;
    }

    public void Dispose()
    {
        _pendingSignal.Dispose();
    }

    internal void RunNext()
    {
        Action invocation;

        lock (_sync)
        {
            invocation = _pendingInvocations.Dequeue();
        }

        invocation();
    }

    internal async Task WaitForPendingAsync(CancellationToken ct)
    {
        await _pendingSignal.WaitAsync(ct).ConfigureAwait(false);
    }

    private void Enqueue(Action invocation)
    {
        lock (_sync)
        {
            _pendingInvocations.Enqueue(invocation);
        }

        _pendingSignal.Release();
    }
}
