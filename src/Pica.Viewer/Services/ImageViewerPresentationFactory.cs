using Microsoft.Extensions.Logging;

namespace Pica.Viewer.Services;

internal sealed class ImageViewerPresentationFactory
{
    private readonly ImagePreviewLoader _imagePreviewLoader;
    private readonly FullResolutionImageLoader _fullResolutionImageLoader;
    private readonly IImageChannelBitmapLoader _imageChannelBitmapLoader;
    private readonly IViewerUiDispatcher _uiDispatcher;
    private readonly ILogger<ImagePresentationController> _presentationLogger;
    private readonly ILogger<ImageLoadCoordinator> _loadLogger;
    private readonly ILogger<ImagePreviewPrefetcher> _previewPrefetcherLogger;

    public ImageViewerPresentationFactory(
        ImagePreviewLoader imagePreviewLoader,
        FullResolutionImageLoader fullResolutionImageLoader,
        IImageChannelBitmapLoader imageChannelBitmapLoader,
        IViewerUiDispatcher uiDispatcher,
        ILogger<ImagePresentationController> presentationLogger,
        ILogger<ImageLoadCoordinator> loadLogger,
        ILogger<ImagePreviewPrefetcher> previewPrefetcherLogger)
    {
        _imagePreviewLoader = imagePreviewLoader
            ?? throw new ArgumentNullException(nameof(imagePreviewLoader));
        _fullResolutionImageLoader = fullResolutionImageLoader
            ?? throw new ArgumentNullException(nameof(fullResolutionImageLoader));
        _imageChannelBitmapLoader = imageChannelBitmapLoader
            ?? throw new ArgumentNullException(nameof(imageChannelBitmapLoader));
        _uiDispatcher = uiDispatcher
            ?? throw new ArgumentNullException(nameof(uiDispatcher));
        _presentationLogger = presentationLogger
            ?? throw new ArgumentNullException(nameof(presentationLogger));
        _loadLogger = loadLogger
            ?? throw new ArgumentNullException(nameof(loadLogger));
        _previewPrefetcherLogger = previewPrefetcherLogger
            ?? throw new ArgumentNullException(nameof(previewPrefetcherLogger));
    }

    internal ImageViewerPresentationServices Create(
        ImageViewerSession session,
        ViewerAnimationFrameScheduler animationFrameScheduler,
        bool isFastLoadingEnabled)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(animationFrameScheduler);
        ImagePresentationController? presentation = null;
        ImageLoadCoordinator? loadCoordinator = null;

        try
        {
            presentation = new ImagePresentationController(
                session,
                _imageChannelBitmapLoader,
                _uiDispatcher,
                _presentationLogger);
            loadCoordinator = new ImageLoadCoordinator(
                session,
                _imagePreviewLoader,
                _fullResolutionImageLoader,
                presentation,
                animationFrameScheduler,
                _uiDispatcher,
                _loadLogger,
                _previewPrefetcherLogger,
                isFastLoadingEnabled);
            ImagePresentationReadiness readiness = new(
                session,
                loadCoordinator,
                presentation);

            return new ImageViewerPresentationServices(
                presentation,
                loadCoordinator,
                readiness);
        }
        catch (Exception)
        {
            loadCoordinator?.Dispose();
            presentation?.Dispose();
            throw;
        }
    }
}
