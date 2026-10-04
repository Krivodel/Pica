using Microsoft.Extensions.Logging;

using Pica.Desktop.Resources;
using Pica.Viewer.Services;

namespace Pica.Desktop.Services;

internal sealed class PicaClipboardShortcutSettingContributionProvider : IViewerSettingContributionProvider
{
    private readonly IPicaDesktopStateService _stateService;
    private readonly PicaClipboardShortcutService _shortcutService;
    private readonly IPicaShortcutRecorder _recorder;
    private readonly ILogger<PicaClipboardShortcutSettingContributionProvider> _logger;

    public PicaClipboardShortcutSettingContributionProvider(IPicaDesktopStateService stateService,
        PicaClipboardShortcutService shortcutService, IPicaShortcutRecorder recorder,
        ILogger<PicaClipboardShortcutSettingContributionProvider> logger)
    {
        _stateService = stateService ?? throw new ArgumentNullException(nameof(stateService));
        _shortcutService = shortcutService ?? throw new ArgumentNullException(nameof(shortcutService));
        _recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<ViewerSettingContribution>> CreateAsync(CancellationToken ct)
    {
        if (!OperatingSystem.IsWindows())
        {
            return Array.Empty<ViewerSettingContribution>();
        }

        PicaDesktopState state = await _stateService.LoadAsync(ct).ConfigureAwait(false);
        _shortcutService.Refresh(state);

        return new ViewerSettingContribution[]
        {
            new ViewerCheckBoxSettingContribution(DesktopLocalization.Get(PicaDesktopLocalizationKeys.ClipboardShortcutTitle),
                state.IsClipboardShortcutEnabled, SetEnabledAsync, _logger, GetErrorMessage,
                dependentSettings: CreateOptions(state),
                getCurrentValue: () => _shortcutService.CurrentState.IsClipboardShortcutEnabled)
            { LocalizationKey = PicaDesktopLocalizationKeys.ClipboardShortcutTitle }
        };
    }

    internal static string GetErrorMessage(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        PicaShortcutFailure? failure = (exception as PicaShortcutException)?.Failure;

        return failure switch
        {
            PicaShortcutFailure.Occupied => DesktopLocalization.Get(PicaDesktopLocalizationKeys.ShortcutOccupied),
            PicaShortcutFailure.ViewerConflict => DesktopLocalization.Get(PicaDesktopLocalizationKeys.ShortcutViewerConflict),
            PicaShortcutFailure.Unsupported => DesktopLocalization.Get(PicaDesktopLocalizationKeys.ShortcutUnsupported),
            _ => DesktopLocalization.Get(PicaDesktopLocalizationKeys.ShortcutSaveFailed)
        };
    }

    internal async Task<IReadOnlyList<ViewerSettingContribution>> CreateOptionsAsync(CancellationToken ct)
    {
        PicaDesktopState state = await _stateService.LoadAsync(ct).ConfigureAwait(false);
        _shortcutService.Refresh(state);

        return CreateOptions(state);
    }

    internal Task EnableAsync(CancellationToken ct)
    {
        return SetEnabledAsync(true, ct);
    }

    private IReadOnlyList<ViewerSettingContribution> CreateOptions(PicaDesktopState state)
    {
        return new ViewerSettingContribution[]
        {
            new ViewerCheckBoxSettingContribution(DesktopLocalization.Get(PicaDesktopLocalizationKeys.ClipboardShortcutFullscreen),
                state.IsFullscreenClipboardShortcutEnabled, SetFullscreenEnabledAsync, _logger, GetErrorMessage,
                wrapContent: true, getCurrentValue: () => _shortcutService.CurrentState.IsFullscreenClipboardShortcutEnabled)
            { LocalizationKey = PicaDesktopLocalizationKeys.ClipboardShortcutFullscreen },
            new ViewerRecordedSettingContribution<PicaClipboardShortcutGesture>(
                DesktopLocalization.Get(PicaDesktopLocalizationKeys.ClipboardShortcutGesture),
                state.ClipboardShortcut, _recorder.RecordAsync, SetGestureAsync, gesture => gesture.Format(),
                GetErrorMessage, _logger, () => _shortcutService.CurrentState.ClipboardShortcut)
            { LocalizationKey = PicaDesktopLocalizationKeys.ClipboardShortcutGesture }
        };
    }

    private async Task SetEnabledAsync(bool enabled, CancellationToken ct)
    {
        await _shortcutService.ApplyAsync(new PicaClipboardAgentRequest(PicaClipboardAgentOperation.SetEnabled,
            IsEnabled: enabled), ct).ConfigureAwait(false);
    }

    private async Task SetGestureAsync(PicaClipboardShortcutGesture gesture, CancellationToken ct)
    {
        await _shortcutService.ApplyAsync(new PicaClipboardAgentRequest(PicaClipboardAgentOperation.SetGesture, gesture), ct)
            .ConfigureAwait(false);
    }

    private async Task SetFullscreenEnabledAsync(bool enabled, CancellationToken ct)
    {
        await _shortcutService.ApplyAsync(new PicaClipboardAgentRequest(PicaClipboardAgentOperation.SetFullscreenEnabled,
            IsEnabled: enabled), ct).ConfigureAwait(false);
    }
}
