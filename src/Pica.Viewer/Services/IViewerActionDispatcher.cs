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
}
