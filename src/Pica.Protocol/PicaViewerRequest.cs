namespace Pica.Protocol;

public sealed record PicaViewerRequest(
    IReadOnlyList<PicaImageItem> Items,
    Guid SelectedItemId,
    IReadOnlyList<PicaActionDefinition>? Actions = null,
    string? ActionPayloadDirectory = null)
{
    public IReadOnlyList<PicaActionDefinition> Actions { get; init; } = Actions ?? [];
}
