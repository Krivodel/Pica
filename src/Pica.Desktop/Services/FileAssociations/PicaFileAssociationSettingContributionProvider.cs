using Microsoft.Extensions.Logging;

using Pica.Desktop.Resources;
using Pica.Viewer.Services;

namespace Pica.Desktop.Services.FileAssociations;

internal sealed class PicaFileAssociationSettingContributionProvider : IViewerSettingContributionProvider
{
    private readonly PicaFileAssociationDialog _dialog;
    private readonly ILogger<PicaFileAssociationSettingContributionProvider> _logger;

    public PicaFileAssociationSettingContributionProvider(PicaFileAssociationDialog dialog,
        ILogger<PicaFileAssociationSettingContributionProvider> logger)
    {
        _dialog = dialog ?? throw new ArgumentNullException(nameof(dialog));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<IReadOnlyList<ViewerSettingContribution>> CreateAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        return Task.FromResult<IReadOnlyList<ViewerSettingContribution>>(new ViewerSettingContribution[]
        {
            new ViewerActionSettingContribution(
                DesktopLocalization.Get(PicaDesktopLocalizationKeys.FileAssociationsTitle), _dialog.ShowAsync,
                _ => DesktopLocalization.Get(PicaDesktopLocalizationKeys.FileAssociationsFailed), _logger)
            { LocalizationKey = PicaDesktopLocalizationKeys.FileAssociationsTitle }
        });
    }
}
