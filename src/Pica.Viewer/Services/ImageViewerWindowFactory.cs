using Pica.Protocol;
using Pica.Viewer.Views;

namespace Pica.Viewer.Services;

internal sealed class ImageViewerWindowFactory : IImageViewerWindowFactory
{
    private readonly IImageViewerStateService _stateService;
    private readonly IViewerUiDispatcher _uiDispatcher;
    private readonly ImageViewerWindowComposer _windowComposer;
    private readonly IReadOnlyList<IViewerSettingContributionProvider>
        _settingContributionProviders;

    public ImageViewerWindowFactory(
        IImageViewerStateService stateService,
        IViewerUiDispatcher uiDispatcher,
        ImageViewerWindowComposer windowComposer,
        IEnumerable<IViewerSettingContributionProvider>
            settingContributionProviders)
    {
        _stateService = stateService
            ?? throw new ArgumentNullException(nameof(stateService));
        _uiDispatcher = uiDispatcher
            ?? throw new ArgumentNullException(nameof(uiDispatcher));
        _windowComposer = windowComposer
            ?? throw new ArgumentNullException(nameof(windowComposer));
        ArgumentNullException.ThrowIfNull(settingContributionProviders);
        _settingContributionProviders =
            settingContributionProviders.ToList();
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
        IReadOnlyList<ViewerSettingContribution> settingContributions =
            await CreateSettingContributionsAsync(ct).ConfigureAwait(false);

        return await _uiDispatcher.InvokeAsync(
            () => _windowComposer.Create(
                request,
                actionDispatcher,
                state,
                settingContributions),
            ct).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<ViewerSettingContribution>>
        CreateSettingContributionsAsync(CancellationToken ct)
    {
        List<ViewerSettingContribution> contributions = [];

        foreach (IViewerSettingContributionProvider provider
            in _settingContributionProviders)
        {
            IReadOnlyList<ViewerSettingContribution>
                providerContributions = await provider
                    .CreateAsync(ct)
                    .ConfigureAwait(false);
            contributions.AddRange(providerContributions);
        }

        return contributions;
    }
}
