using Microsoft.Extensions.Logging;

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
                option.DisplayName))
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
                "Оставаться в фоне после закрытия",
                TimeoutChoices,
                state.BackgroundIdleTimeoutSeconds,
                ChangeBackgroundIdleTimeoutAsync);

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
            PicaDesktopState state = await _stateService
                .LoadAsync(ct)
                .ConfigureAwait(false);
            state.BackgroundIdleTimeoutSeconds = timeoutSeconds;
            await _stateService
                .SaveAsync(state, ct)
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
