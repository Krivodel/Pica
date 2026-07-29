using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.TestDoubles;

internal sealed class ThrowingImageViewerStateService :
    IImageViewerStateService
{
    internal static readonly InvalidOperationException SaveException =
        new("State save failed.");

    public Task<ImageViewerState> LoadAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        return Task.FromResult(new ImageViewerState());
    }

    public Task SaveAsync(
        ImageViewerState state,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(state);
        ct.ThrowIfCancellationRequested();

        return Task.FromException(SaveException);
    }
}
