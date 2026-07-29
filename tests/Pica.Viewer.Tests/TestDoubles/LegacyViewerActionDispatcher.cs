using Pica.Protocol;
using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.TestDoubles;

internal sealed class LegacyViewerActionDispatcher : IViewerActionDispatcher
{
    internal int SelectionDispatchCount { get; private set; }
    internal byte[]? LastPngContent { get; private set; }

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
        SelectionDispatchCount++;
        LastPngContent = pngContent;

        return Task.CompletedTask;
    }
}
