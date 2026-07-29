using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.TestDoubles;

internal sealed class BlockingImageViewerStateService :
    IImageViewerStateService
{
    private readonly TaskCompletionSource _saveStarted =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _saveCompletion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal Task SaveStarted => _saveStarted.Task;

    public Task<ImageViewerState> LoadAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        return Task.FromResult(new ImageViewerState());
    }

    public async Task SaveAsync(
        ImageViewerState state,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(state);
        _saveStarted.TrySetResult();
        await _saveCompletion.Task.WaitAsync(ct).ConfigureAwait(false);
    }

    internal void CompleteSave()
    {
        _saveCompletion.TrySetResult();
    }
}
