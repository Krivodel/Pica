using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.TestDoubles;

internal sealed class DelegateClipboardSnapshotReader : IPlatformClipboardSnapshotReader
{
    internal Func<ClipboardDataSnapshot> Capture { get; set; } = () => new ClipboardDataSnapshot();

    public Task<ClipboardDataSnapshot> ReadAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        return Task.FromResult(Capture());
    }
}
