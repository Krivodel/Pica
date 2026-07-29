using Pica.Viewer.ViewModels;

namespace Pica.Viewer.Services;

internal sealed class ImageViewerSettingsFactory
{
    private readonly IImageViewerStateService _stateService;
    private readonly IImageFileMetadataProvider _metadataProvider;
    private readonly IViewModelErrorHandler _errorHandler;

    public ImageViewerSettingsFactory(
        IImageViewerStateService stateService,
        IImageFileMetadataProvider metadataProvider,
        IViewModelErrorHandler errorHandler)
    {
        _stateService = stateService
            ?? throw new ArgumentNullException(nameof(stateService));
        _metadataProvider = metadataProvider
            ?? throw new ArgumentNullException(nameof(metadataProvider));
        _errorHandler = errorHandler
            ?? throw new ArgumentNullException(nameof(errorHandler));
    }

    internal ImageViewerSettingsServices Create(
        ImageViewerSession session,
        ImageViewerSessionViewModel sessionViewModel,
        ImagePresentationController presentation,
        IImageLoadingSettings imageLoadingSettings,
        IViewerWindowPlacementProvider windowPlacementProvider,
        ImageViewerState state)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(sessionViewModel);
        ArgumentNullException.ThrowIfNull(presentation);
        ArgumentNullException.ThrowIfNull(imageLoadingSettings);
        ArgumentNullException.ThrowIfNull(windowPlacementProvider);
        ArgumentNullException.ThrowIfNull(state);
        ImageViewerSettingsViewModel? settings = null;
        ImageViewerInformationViewModel? information = null;

        try
        {
            settings = new ImageViewerSettingsViewModel(
                _stateService,
                session,
                imageLoadingSettings,
                windowPlacementProvider,
                _errorHandler,
                state);
            information = new ImageViewerInformationViewModel(
                session,
                presentation,
                _metadataProvider,
                settings,
                _errorHandler);
            information.Start();
            ImageViewerToolMenuViewModel toolMenu = new(
                sessionViewModel,
                settings);

            return new ImageViewerSettingsServices(
                settings,
                information,
                toolMenu);
        }
        catch (Exception)
        {
            information?.Dispose();
            settings?.Dispose();
            throw;
        }
    }
}
