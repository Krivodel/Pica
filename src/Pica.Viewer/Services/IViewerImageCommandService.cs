using Pica.Protocol;

namespace Pica.Viewer.Services;

internal interface IViewerImageCommandService
{
    string? PreparedOpenWithFilePath { get; }

    event EventHandler? PreparedSelectionSaved;

    Task CopyCurrentAsync(CancellationToken ct);

    Task CopyPreparedImageAsync(
        PreparedClipboardImage image,
        CancellationToken ct);

    Task<PreparedClipboardImage?> PrepareSelectionAsync(
        ImagePixelSelection selection,
        CancellationToken ct);

    Task DispatchCurrentAsync(
        PicaActionDefinition action,
        CancellationToken ct);

    Task DispatchPreparedSelectionAsync(
        PicaActionDefinition action,
        PicaImageItem item,
        PreparedClipboardImage image,
        CancellationToken ct);

    Task SaveCurrentAsync(CancellationToken ct);

    Task SavePreparedSelectionAsync(
        PreparedClipboardImage image,
        CancellationToken ct);

    string GetCurrentOpenWithAssociationFilePath();

    Task PrepareCurrentOpenWithFileAsync(CancellationToken ct);

    Task PrepareSelectionOpenWithFileAsync(
        PreparedClipboardImage image,
        CancellationToken ct);
}
