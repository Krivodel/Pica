namespace Pica.Desktop.Services;

internal interface IPicaDesktopStateService
{
    Task UpdateAsync(Action<PicaDesktopState> update, CancellationToken ct);
    Task UpdateAsync(Func<PicaDesktopState, CancellationToken, Task> update, Func<Task> rollback, CancellationToken ct);

    Task<PicaDesktopState> LoadAsync(CancellationToken ct);
    Task SaveAsync(PicaDesktopState state, CancellationToken ct);
}
