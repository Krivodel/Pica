namespace Pica.Desktop.Services;

internal interface IPicaDesktopStateService
{
    Task<PicaDesktopState> LoadAsync(CancellationToken ct);
    Task SaveAsync(PicaDesktopState state, CancellationToken ct);
}
