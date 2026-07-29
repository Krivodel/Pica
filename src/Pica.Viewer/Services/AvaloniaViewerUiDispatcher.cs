using Avalonia.Threading;

namespace Pica.Viewer.Services;

internal sealed class AvaloniaViewerUiDispatcher : IViewerUiDispatcher
{
    public async Task InvokeAsync(
        Action action,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(action);

        await Dispatcher.UIThread.InvokeAsync(
            action,
            DispatcherPriority.Normal,
            ct);
    }

    public async Task<TResult> InvokeAsync<TResult>(
        Func<TResult> action,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(action);

        return await Dispatcher.UIThread.InvokeAsync(
            action,
            DispatcherPriority.Normal,
            ct);
    }
}
