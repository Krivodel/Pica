using Pica.Protocol;
using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.TestDoubles;

internal sealed class RecordingViewerActionDispatcher : IViewerActionDispatcher
{
    internal int CurrentImageDispatchCount { get; private set; }
    internal int SelectionDispatchCount { get; private set; }
    internal int DerivedImageDispatchCount { get; private set; }
    internal string? LastFileName { get; private set; }
    internal byte[]? LastPngContent { get; private set; }

    public Task DispatchCurrentImageAsync(
        PicaActionDefinition action,
        PicaImageItem item,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(item);
        ct.ThrowIfCancellationRequested();
        CurrentImageDispatchCount++;

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

    public Task DispatchDerivedImageAsync(
        PicaActionDefinition action,
        PicaImageItem item,
        string fileName,
        byte[] pngContent,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(item);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(pngContent);
        ct.ThrowIfCancellationRequested();
        DerivedImageDispatchCount++;
        LastFileName = fileName;
        LastPngContent = pngContent;

        return Task.CompletedTask;
    }
}
