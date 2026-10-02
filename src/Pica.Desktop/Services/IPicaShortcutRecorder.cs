namespace Pica.Desktop.Services;

internal interface IPicaShortcutRecorder
{
    Task<PicaClipboardShortcutGesture> RecordAsync(nint owner, CancellationToken ct);
}
