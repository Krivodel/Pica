using Microsoft.Extensions.Logging;

using Pica.Viewer.Services;
using Pica.Viewer.Views;
using Pica.Desktop.Services.Background;

namespace Pica.Desktop.Services;

internal sealed class PicaDesktopViewerWindowFactory
{
    private readonly IImageFormatRegistry _formatRegistry;
    private readonly IImageViewerWindowFactory _windowFactory;
    private readonly ILogger<ViewerActionDispatcher> _actionLogger;
    private readonly IPicaDesktopStateService _stateService;
    private readonly PicaClipboardShortcutService _shortcutService;
    private readonly ILogger<PicaWindowClipboardActivation> _activationLogger;

    public PicaDesktopViewerWindowFactory(
        IImageFormatRegistry formatRegistry,
        IImageViewerWindowFactory windowFactory,
        ILogger<ViewerActionDispatcher> actionLogger,
        IPicaDesktopStateService stateService,
        PicaClipboardShortcutService shortcutService,
        ILogger<PicaWindowClipboardActivation> activationLogger)
    {
        _formatRegistry = formatRegistry
            ?? throw new ArgumentNullException(nameof(formatRegistry));
        _windowFactory = windowFactory
            ?? throw new ArgumentNullException(nameof(windowFactory));
        _actionLogger = actionLogger
            ?? throw new ArgumentNullException(nameof(actionLogger));
        _stateService = stateService ?? throw new ArgumentNullException(nameof(stateService));
        _shortcutService = shortcutService ?? throw new ArgumentNullException(nameof(shortcutService));
        _activationLogger = activationLogger ?? throw new ArgumentNullException(nameof(activationLogger));
    }

    public async Task<ImageViewerWindow> CreateAsync(
        PicaStartupRequest startupRequest,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(startupRequest);
        ViewerActionDispatcher actionDispatcher = new(
            startupRequest.HostConnection,
            _formatRegistry,
            _actionLogger,
            startupRequest.ViewerRequest.ActionPayloadDirectory);

        ImageViewerWindow window = await _windowFactory.CreateAsync(
            startupRequest.ViewerRequest,
            actionDispatcher,
            ct);

        if (OperatingSystem.IsWindows() && (startupRequest.HostConnection is null))
        {
            PicaDesktopState state = await _stateService.LoadAsync(ct);
            _shortcutService.Refresh(state);
            window.SetClipboardShortcut(state.IsClipboardShortcutEnabled ? state.ClipboardShortcut : null);
            PicaWindowClipboardActivation activation = new(window, _stateService, _shortcutService, _activationLogger);
            window.Closed += (_, _) => activation.Dispose();

            if (state.IsClipboardShortcutEnabled)
            {
                try
                {
                    await _shortcutService.ApplyAsync(new PicaClipboardAgentRequest(PicaClipboardAgentOperation.Initialize), ct);
                }
                catch (Exception ex)
                {
                    _activationLogger.LogWarning(ex, "Pica could not restore the global clipboard shortcut");
                }
            }
        }

        return window;
    }
}
