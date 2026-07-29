using Pica.Protocol;
using Pica.Viewer.Views;

namespace Pica.Viewer.Services;

internal sealed class ImageViewerWindowFactory : IImageViewerWindowFactory
{
    private readonly IImageViewerStateService _stateService;
    private readonly IViewerUiDispatcher _uiDispatcher;
    private readonly ImageViewerWindowComposer _windowComposer;

    public ImageViewerWindowFactory(
        IImageViewerStateService stateService,
        IViewerUiDispatcher uiDispatcher,
        ImageViewerWindowComposer windowComposer)
    {
        _stateService = stateService
            ?? throw new ArgumentNullException(nameof(stateService));
        _uiDispatcher = uiDispatcher
            ?? throw new ArgumentNullException(nameof(uiDispatcher));
        _windowComposer = windowComposer
            ?? throw new ArgumentNullException(nameof(windowComposer));
    }

    public async Task<ImageViewerWindow> CreateAsync(
        PicaViewerRequest request,
        IViewerActionDispatcher actionDispatcher,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actionDispatcher);
        ImageViewerState state = await _stateService
            .LoadAsync(ct)
            .ConfigureAwait(false);

        return await _uiDispatcher.InvokeAsync(
            () => _windowComposer.Create(
                request,
                actionDispatcher,
                state),
            ct).ConfigureAwait(false);
    }
}
