using Pica.Desktop.Services.Background;

namespace Pica.Desktop.Tests.Services.Background;

internal sealed class RecordingIdleMemoryReclaimer :
    IPicaIdleMemoryReclaimer
{
    internal int CallCount { get; private set; }

    public Task ReclaimAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        CallCount++;

        return Task.CompletedTask;
    }
}
