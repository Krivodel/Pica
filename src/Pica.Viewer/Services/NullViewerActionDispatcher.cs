using Pica.Protocol;

namespace Pica.Viewer.Services;

internal sealed class NullViewerActionDispatcher : IViewerActionDispatcher
{
    internal static NullViewerActionDispatcher Instance { get; } = new();

    private NullViewerActionDispatcher()
    {
    }

    public Task DispatchCurrentImageAsync(
        PicaActionDefinition action,
        PicaImageItem item,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(item);
        ct.ThrowIfCancellationRequested();

        return Task.CompletedTask;
    }

    public Task DispatchSelectionAsync(
        PicaActionDefinition action,
        PicaImageItem item,
        byte[] pngContent,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(pngContent);
        ct.ThrowIfCancellationRequested();

        return Task.CompletedTask;
    }
}
