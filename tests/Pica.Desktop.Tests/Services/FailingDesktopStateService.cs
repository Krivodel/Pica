using Pica.Desktop.Services;

namespace Pica.Desktop.Tests.Services;

internal sealed class FailingDesktopStateService : IPicaDesktopStateService
{
    private readonly PicaDesktopState _state;

    internal FailingDesktopStateService(PicaDesktopState state)
    {
        _state = state;
    }

    public Task<PicaDesktopState> LoadAsync(CancellationToken ct)
    {
        return Task.FromResult(_state.CreateCopy());
    }

    public Task SaveAsync(PicaDesktopState state, CancellationToken ct)
    {
        return Task.FromException(new IOException("Test state write failed."));
    }

    public Task UpdateAsync(Action<PicaDesktopState> update, CancellationToken ct)
    {
        return Task.FromException(new IOException("Test state update failed."));
    }

    public async Task UpdateAsync(Func<PicaDesktopState, CancellationToken, Task> update, Func<Task> rollback, CancellationToken ct)
    {
        try
        {
            await update(_state.CreateCopy(), ct).ConfigureAwait(false);
            throw new IOException("Test state update failed.");
        }
        catch (Exception)
        {
            await rollback().ConfigureAwait(false);
            throw;
        }
    }
}
