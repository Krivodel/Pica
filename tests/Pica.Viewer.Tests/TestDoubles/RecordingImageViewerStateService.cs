using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.TestDoubles;

internal sealed class RecordingImageViewerStateService : IImageViewerStateService
{
    internal ImageViewerState? LastSavedState { get; private set; }
    internal int SaveCount { get; private set; }

    private readonly ImageViewerState _state;

    internal RecordingImageViewerStateService(ImageViewerState state)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
    }

    public Task<ImageViewerState> LoadAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        return Task.FromResult(_state);
    }

    public Task SaveAsync(
        ImageViewerState state,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(state);
        ct.ThrowIfCancellationRequested();
        LastSavedState = state.CreateCopy();
        SaveCount++;

        return Task.CompletedTask;
    }
}
