using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.TestDoubles;

internal sealed class InlineViewerUiDispatcher : IViewerUiDispatcher
{
    public Task InvokeAsync(
        Action action,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(action);
        ct.ThrowIfCancellationRequested();
        action();

        return Task.CompletedTask;
    }

    public Task<TResult> InvokeAsync<TResult>(
        Func<TResult> action,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(action);
        ct.ThrowIfCancellationRequested();

        return Task.FromResult(action());
    }
}
