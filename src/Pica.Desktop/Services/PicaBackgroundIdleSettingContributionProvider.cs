using Microsoft.Extensions.Logging;

using Pica.Desktop.Resources;
using Pica.Viewer.Services;

namespace Pica.Desktop.Services;

internal sealed class PicaBackgroundIdleSettingContributionProvider :
    IViewerSettingContributionProvider
{
    private static IReadOnlyList<ViewerSettingChoice<int>>
        TimeoutChoices { get; } =
        PicaBackgroundIdleTimeoutSettings.Options
            .Select(option => new ViewerSettingChoice<int>(
                option.TimeoutSeconds,
                DesktopLocalization.Get(option.LocalizationKey)) { LocalizationKey = option.LocalizationKey })
            .ToArray();

    private readonly IPicaDesktopStateService _stateService;
    private readonly ILogger<PicaBackgroundIdleSettingContributionProvider>
        _logger;

    public PicaBackgroundIdleSettingContributionProvider(
        IPicaDesktopStateService stateService,
        ILogger<PicaBackgroundIdleSettingContributionProvider> logger)
    {
        _stateService = stateService
            ?? throw new ArgumentNullException(nameof(stateService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<ViewerSettingContribution>> CreateAsync(
        CancellationToken ct)
    {
        PicaDesktopState state;

        try
        {
            state = await _stateService
                .LoadAsync(ct)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Pica could not load the background idle timeout; using the default");
            state = new PicaDesktopState();
        }

        ViewerSettingContribution contribution =
            new ViewerChoiceSettingContribution<int>(
                DesktopLocalization.Get(PicaDesktopLocalizationKeys.BackgroundIdle),
                TimeoutChoices,
                state.BackgroundIdleTimeoutSeconds,
                ChangeBackgroundIdleTimeoutAsync) { LocalizationKey = PicaDesktopLocalizationKeys.BackgroundIdle };

        return new List<ViewerSettingContribution>
        {
            contribution
        };
    }

    private async Task ChangeBackgroundIdleTimeoutAsync(
        int timeoutSeconds,
        CancellationToken ct)
    {
        try
        {
            await _stateService
                .UpdateAsync(state => state.BackgroundIdleTimeoutSeconds = timeoutSeconds, ct)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Pica could not save the background idle timeout");
        }
    }
}
