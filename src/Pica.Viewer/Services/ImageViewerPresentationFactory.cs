using Microsoft.Extensions.Logging;

namespace Pica.Viewer.Services;

internal sealed class ImageViewerPresentationFactory
{
    private readonly IImagePreviewLoader _imagePreviewLoader;
    private readonly IFullResolutionImageLoader _fullResolutionImageLoader;
    private readonly IImageChannelBitmapLoader _imageChannelBitmapLoader;
    private readonly IImageAnimationDelayScheduler _animationDelayScheduler;
    private readonly IViewerUiDispatcher _uiDispatcher;
    private readonly ILogger<ImagePresentationController> _presentationLogger;
    private readonly ILogger<ImageAnimationPlaybackController>
        _animationPlaybackLogger;
    private readonly ILogger<ImageLoadCoordinator> _loadLogger;
    private readonly ILogger<ImagePreviewPrefetcher> _previewPrefetcherLogger;

    public ImageViewerPresentationFactory(
        IImagePreviewLoader imagePreviewLoader,
        IFullResolutionImageLoader fullResolutionImageLoader,
        IImageChannelBitmapLoader imageChannelBitmapLoader,
        IImageAnimationDelayScheduler animationDelayScheduler,
        IViewerUiDispatcher uiDispatcher,
        ILogger<ImagePresentationController> presentationLogger,
        ILogger<ImageAnimationPlaybackController> animationPlaybackLogger,
        ILogger<ImageLoadCoordinator> loadLogger,
        ILogger<ImagePreviewPrefetcher> previewPrefetcherLogger)
    {
        _imagePreviewLoader = imagePreviewLoader
            ?? throw new ArgumentNullException(nameof(imagePreviewLoader));
        _fullResolutionImageLoader = fullResolutionImageLoader
            ?? throw new ArgumentNullException(nameof(fullResolutionImageLoader));
        _imageChannelBitmapLoader = imageChannelBitmapLoader
            ?? throw new ArgumentNullException(nameof(imageChannelBitmapLoader));
        _animationDelayScheduler = animationDelayScheduler
            ?? throw new ArgumentNullException(nameof(animationDelayScheduler));
        _uiDispatcher = uiDispatcher
            ?? throw new ArgumentNullException(nameof(uiDispatcher));
        _presentationLogger = presentationLogger
            ?? throw new ArgumentNullException(nameof(presentationLogger));
        _animationPlaybackLogger = animationPlaybackLogger
            ?? throw new ArgumentNullException(nameof(animationPlaybackLogger));
        _loadLogger = loadLogger
            ?? throw new ArgumentNullException(nameof(loadLogger));
        _previewPrefetcherLogger = previewPrefetcherLogger
            ?? throw new ArgumentNullException(nameof(previewPrefetcherLogger));
    }

    internal ImageViewerPresentationServices Create(
        ImageViewerSession session,
        IViewerRenderFrameAwaiter renderFrameAwaiter,
        bool isFastLoadingEnabled)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(renderFrameAwaiter);
        ImagePresentationController? presentation = null;
        ImageAnimationPlaybackController? animationPlayback = null;
        ImageLoadCoordinator? loadCoordinator = null;

        try
        {
            presentation = new ImagePresentationController(
                session,
                _imageChannelBitmapLoader,
                _uiDispatcher,
                _presentationLogger);
            animationPlayback = new ImageAnimationPlaybackController(
                session,
                presentation,
                _animationDelayScheduler,
                _uiDispatcher,
                _animationPlaybackLogger);
            loadCoordinator = new ImageLoadCoordinator(
                session,
                _imagePreviewLoader,
                _fullResolutionImageLoader,
                presentation,
                renderFrameAwaiter,
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
                animationPlayback,
                loadCoordinator,
                readiness);
        }
        catch (Exception)
        {
            loadCoordinator?.Dispose();
            animationPlayback?.Dispose();
            presentation?.Dispose();
            throw;
        }
    }
}
