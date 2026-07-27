namespace Pica.Viewer.Services;

public interface IImageViewerStateService
{
    Task<ImageViewerState> LoadAsync(CancellationToken ct);

    Task SaveAsync(ImageViewerState state, CancellationToken ct);
}
