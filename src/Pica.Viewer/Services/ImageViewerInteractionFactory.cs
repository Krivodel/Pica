using Microsoft.Extensions.Logging;

using Pica.Protocol;
using Pica.Viewer.ViewModels;

namespace Pica.Viewer.Services;

internal sealed class ImageViewerInteractionFactory
{
    private readonly ViewerClipboardFactory _clipboardFactory;
    private readonly IImageFormatRegistry _formatRegistry;
    private readonly IViewerUiDispatcher _uiDispatcher;
    private readonly PngImageEncoder _pngImageEncoder;
    private readonly ClipboardImagePreparer _clipboardImagePreparer;
    private readonly IPlatformFileActions _platformFileActions;
    private readonly IViewModelErrorHandler _errorHandler;
    private readonly ILogger<ImageViewerActionsViewModel> _actionsLogger;
    private readonly ILogger<ImageViewerOpenWithViewModel> _openWithLogger;
    private readonly ILogger<TemporaryImageFileStore> _temporaryFileLogger;

    public ImageViewerInteractionFactory(
        ViewerClipboardFactory clipboardFactory,
        IImageFormatRegistry formatRegistry,
        IViewerUiDispatcher uiDispatcher,
        PngImageEncoder pngImageEncoder,
        ClipboardImagePreparer clipboardImagePreparer,
        IPlatformFileActions platformFileActions,
        IViewModelErrorHandler errorHandler,
        ILogger<ImageViewerActionsViewModel> actionsLogger,
        ILogger<ImageViewerOpenWithViewModel> openWithLogger,
        ILogger<TemporaryImageFileStore> temporaryFileLogger)
    {
        _clipboardFactory = clipboardFactory
            ?? throw new ArgumentNullException(nameof(clipboardFactory));
        _formatRegistry = formatRegistry
            ?? throw new ArgumentNullException(nameof(formatRegistry));
        _uiDispatcher = uiDispatcher
            ?? throw new ArgumentNullException(nameof(uiDispatcher));
        _pngImageEncoder = pngImageEncoder
            ?? throw new ArgumentNullException(nameof(pngImageEncoder));
        _clipboardImagePreparer = clipboardImagePreparer
            ?? throw new ArgumentNullException(nameof(clipboardImagePreparer));
        _platformFileActions = platformFileActions
            ?? throw new ArgumentNullException(nameof(platformFileActions));
        _errorHandler = errorHandler
            ?? throw new ArgumentNullException(nameof(errorHandler));
        _actionsLogger = actionsLogger
            ?? throw new ArgumentNullException(nameof(actionsLogger));
        _openWithLogger = openWithLogger
            ?? throw new ArgumentNullException(nameof(openWithLogger));
        _temporaryFileLogger = temporaryFileLogger
            ?? throw new ArgumentNullException(nameof(temporaryFileLogger));
    }

    internal ImageViewerInteractionServices Create(
        ImageViewerSession session,
        ImagePresentationController presentation,
        IImagePresentationReadiness presentationReadiness,
        ViewerWindowPlatformContext platformContext,
        IViewerActionDispatcher actionDispatcher)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(presentation);
        ArgumentNullException.ThrowIfNull(presentationReadiness);
        ArgumentNullException.ThrowIfNull(platformContext);
        ArgumentNullException.ThrowIfNull(actionDispatcher);
        ViewerClipboardServices? clipboardServices = null;
        TemporaryImageFileStore? temporaryFileStore = null;
        ViewerImageCommandService? commandService = null;
        ImageViewerActionsViewModel? actions = null;

        try
        {
            clipboardServices = _clipboardFactory.Create(platformContext);
            AvaloniaViewerFilePickerService filePickerService = new(
                _uiDispatcher,
                platformContext);
            ViewerImageOperations imageOperations = new(
                clipboardServices.Writer,
                filePickerService,
                _formatRegistry,
                _pngImageEncoder,
                actionDispatcher);
            temporaryFileStore = new TemporaryImageFileStore(
                _temporaryFileLogger);
            commandService = new ViewerImageCommandService(
                session,
                presentation,
                presentationReadiness,
                imageOperations,
                _clipboardImagePreparer,
                temporaryFileStore);
            actions = new ImageViewerActionsViewModel(
                commandService,
                presentation,
                session,
                _platformFileActions,
                _errorHandler,
                _actionsLogger);
            ImageViewerOpenWithViewModel openWith = new(
                commandService,
                _platformFileActions,
                _errorHandler,
                _openWithLogger);

            return new ImageViewerInteractionServices(
                actions,
                openWith,
                commandService,
                clipboardServices);
        }
        catch (Exception)
        {
            actions?.Dispose();
            commandService?.Dispose();

            if (commandService is null)
            {
                temporaryFileStore?.Dispose();
            }

            clipboardServices?.Dispose();
            throw;
        }
    }
}
