namespace Pica.Desktop.Services.Background;

internal interface IPicaIdleMemoryReclaimer
{
    Task ReclaimAsync(CancellationToken ct);
}
