using Pica.Desktop.Resources;
using Pica.Viewer.Services;
using Pica.Viewer.ViewModels;

namespace Pica.Desktop.Services;

internal sealed class PicaLanguageSettingContributionProvider : IViewerSettingContributionProvider
{
    private readonly PicaLocalizationService _localization;
    private readonly IViewModelErrorHandler _errorHandler;

    public PicaLanguageSettingContributionProvider(PicaLocalizationService localization,
        IViewModelErrorHandler errorHandler)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
    }

    public async Task<IReadOnlyList<ViewerSettingContribution>> CreateAsync(CancellationToken ct)
    {
        await _localization.InitializeAsync(ct);

        return new ViewerSettingContribution[]
        {
            new ViewerLanguageSettingContribution(
                DesktopLocalization.Get(PicaDesktopLocalizationKeys.Language), _localization.Localization,
                _localization.SelectAsync, _errorHandler,
                PicaDesktopLocalizationKeys.LanguageSearch)
            {
                LocalizationKey = PicaDesktopLocalizationKeys.Language,
                Placement = ViewerSettingPlacement.Header
            }
        };
    }
}
