namespace Pica.Desktop.Services.Background;

internal sealed class PicaBackgroundIdlePreparation
{
    private readonly IPicaIdleMemoryReclaimer _memoryReclaimer;

    public PicaBackgroundIdlePreparation(
        IPicaIdleMemoryReclaimer memoryReclaimer)
    {
        _memoryReclaimer = memoryReclaimer
            ?? throw new ArgumentNullException(nameof(memoryReclaimer));
    }

    public async Task PrepareAsync(
        Task closeCleanupCompletion,
        Task activationCompletion,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(closeCleanupCompletion);
        ArgumentNullException.ThrowIfNull(activationCompletion);

        await closeCleanupCompletion
            .WaitAsync(ct)
            .ConfigureAwait(false);

        if (activationCompletion.IsCompleted)
        {
            return;
        }

        await _memoryReclaimer
            .ReclaimAsync(ct)
            .ConfigureAwait(false);
    }
}
