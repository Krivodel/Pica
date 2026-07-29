namespace Pica.Viewer.Services;

internal interface IViewerUiDispatcher
{
    Task InvokeAsync(
        Action action,
        CancellationToken ct);

    Task<TResult> InvokeAsync<TResult>(
        Func<TResult> action,
        CancellationToken ct);
}
