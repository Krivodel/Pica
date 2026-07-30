using System.Runtime;

namespace Pica.Desktop.Services.Background;

internal sealed class PicaIdleMemoryReclaimer :
    IPicaIdleMemoryReclaimer
{
    public async Task ReclaimAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        await Task.Run(
            () => Reclaim(ct),
            ct).ConfigureAwait(false);
    }

    private static void Reclaim(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        GCSettings.LargeObjectHeapCompactionMode =
            GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(
            GC.MaxGeneration,
            GCCollectionMode.Forced,
            true,
            true);
        GC.WaitForPendingFinalizers();
    }
}
