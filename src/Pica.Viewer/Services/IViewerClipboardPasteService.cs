namespace Pica.Viewer.Services;

internal interface IViewerClipboardPasteService : IDisposable
{
    Task PasteAsync(CancellationToken ct);
}
