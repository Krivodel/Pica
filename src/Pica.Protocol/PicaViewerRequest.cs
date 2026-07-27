namespace Pica.Protocol;

public sealed record PicaViewerRequest(
    IReadOnlyList<PicaImageItem> Items,
    Guid SelectedItemId,
    IReadOnlyList<PicaActionDefinition> Actions,
    string? ActionPayloadDirectory);
