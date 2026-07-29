using Pica.Protocol;

namespace Pica.Viewer.Services;

public interface IViewerActionDispatcher
{
    Task DispatchCurrentImageAsync(
        PicaActionDefinition action,
        PicaImageItem item,
        CancellationToken ct);

    Task DispatchSelectionAsync(
        PicaActionDefinition action,
        PicaImageItem item,
        byte[] pngContent,
        CancellationToken ct);

    Task DispatchDerivedImageAsync(
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

        return DispatchSelectionAsync(
            action,
            item,
            pngContent,
            ct);
    }
}
