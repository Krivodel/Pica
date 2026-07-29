using Microsoft.Extensions.Logging;

using Pica.Protocol;
using Pica.Viewer.Views;

namespace Pica.Viewer.Services;

internal sealed class ImageViewerWindowFactory : IImageViewerWindowFactory
{
    private readonly IViewerClipboardWriter _clipboardImageWriter;
    private readonly IImageFormatRegistry _formatRegistry;
    private readonly IImageViewerStateService _stateService;
    private readonly ImagePreviewLoader _imagePreviewLoader;
    private readonly FullResolutionImageLoader _fullResolutionImageLoader;
    private readonly ImageChannelBitmapLoader _imageChannelBitmapLoader;
    private readonly PngImageEncoder _pngImageEncoder;
    private readonly ClipboardImagePreparer _clipboardImagePreparer;
    private readonly IPlatformFileActions _platformFileActions;
    private readonly ILogger<ImageViewerWindow> _logger;
    private readonly ILogger<TemporaryImageFileStore> _temporaryImageFileLogger;

    public ImageViewerWindowFactory(
        IViewerClipboardWriter clipboardImageWriter,
        IImageFormatRegistry formatRegistry,
        IImageViewerStateService stateService,
        ImagePreviewLoader imagePreviewLoader,
        FullResolutionImageLoader fullResolutionImageLoader,
        ImageChannelBitmapLoader imageChannelBitmapLoader,
        PngImageEncoder pngImageEncoder,
        ClipboardImagePreparer clipboardImagePreparer,
        IPlatformFileActions platformFileActions,
        ILogger<ImageViewerWindow> logger,
        ILogger<TemporaryImageFileStore> temporaryImageFileLogger)
    {
        _clipboardImageWriter = clipboardImageWriter
            ?? throw new ArgumentNullException(nameof(clipboardImageWriter));
        _formatRegistry = formatRegistry ?? throw new ArgumentNullException(nameof(formatRegistry));
        _stateService = stateService ?? throw new ArgumentNullException(nameof(stateService));
        _imagePreviewLoader = imagePreviewLoader
            ?? throw new ArgumentNullException(nameof(imagePreviewLoader));
        _fullResolutionImageLoader = fullResolutionImageLoader
            ?? throw new ArgumentNullException(nameof(fullResolutionImageLoader));
        _imageChannelBitmapLoader = imageChannelBitmapLoader
            ?? throw new ArgumentNullException(nameof(imageChannelBitmapLoader));
        _pngImageEncoder = pngImageEncoder
            ?? throw new ArgumentNullException(nameof(pngImageEncoder));
        _clipboardImagePreparer = clipboardImagePreparer
            ?? throw new ArgumentNullException(nameof(clipboardImagePreparer));
        _platformFileActions = platformFileActions
            ?? throw new ArgumentNullException(nameof(platformFileActions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _temporaryImageFileLogger = temporaryImageFileLogger
            ?? throw new ArgumentNullException(nameof(temporaryImageFileLogger));
    }

    public async Task<ImageViewerWindow> CreateAsync(
        PicaViewerRequest request,
        IViewerActionDispatcher actionDispatcher,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actionDispatcher);

        ImageViewerState state = await _stateService.LoadAsync(ct);
        TemporaryImageFileStore temporaryImageFileStore =
            new(_temporaryImageFileLogger);

        return new ImageViewerWindow(
            request,
            _clipboardImageWriter,
            _formatRegistry,
            _stateService,
            _imagePreviewLoader,
            _fullResolutionImageLoader,
            _imageChannelBitmapLoader,
            _pngImageEncoder,
            _clipboardImagePreparer,
            temporaryImageFileStore,
            _platformFileActions,
            actionDispatcher,
            _logger,
            state);
    }
}
