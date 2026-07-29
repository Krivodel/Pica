namespace Pica.Viewer.Services;

internal sealed class ImageViewerPresentationServices : IDisposable
{
    internal ImagePresentationController Presentation { get; }
    internal ImageLoadCoordinator LoadCoordinator { get; }
    internal IImagePresentationReadiness Readiness { get; }

    internal ImageViewerPresentationServices(
        ImagePresentationController presentation,
        ImageLoadCoordinator loadCoordinator,
        IImagePresentationReadiness readiness)
    {
        Presentation = presentation
            ?? throw new ArgumentNullException(nameof(presentation));
        LoadCoordinator = loadCoordinator
            ?? throw new ArgumentNullException(nameof(loadCoordinator));
        Readiness = readiness
            ?? throw new ArgumentNullException(nameof(readiness));
    }

    public void Dispose()
    {
        LoadCoordinator.Dispose();
        Presentation.Dispose();
    }
}
