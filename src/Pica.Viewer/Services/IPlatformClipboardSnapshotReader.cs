namespace Pica.Viewer.Services;

internal interface IPlatformClipboardSnapshotReader
{
    Task<ClipboardDataSnapshot> ReadAsync(CancellationToken ct);
}
