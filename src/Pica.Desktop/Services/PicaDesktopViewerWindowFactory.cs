using Microsoft.Extensions.Logging;

using Pica.Viewer.Services;
using Pica.Viewer.Views;

namespace Pica.Desktop.Services;

internal sealed class PicaDesktopViewerWindowFactory
{
    private readonly IImageFormatRegistry _formatRegistry;
    private readonly IImageViewerWindowFactory _windowFactory;
    private readonly ILogger<ViewerActionDispatcher> _actionLogger;

    public PicaDesktopViewerWindowFactory(
        IImageFormatRegistry formatRegistry,
        IImageViewerWindowFactory windowFactory,
        ILogger<ViewerActionDispatcher> actionLogger)
    {
        _formatRegistry = formatRegistry
            ?? throw new ArgumentNullException(nameof(formatRegistry));
        _windowFactory = windowFactory
            ?? throw new ArgumentNullException(nameof(windowFactory));
        _actionLogger = actionLogger
            ?? throw new ArgumentNullException(nameof(actionLogger));
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

        return await _windowFactory.CreateAsync(
            startupRequest.ViewerRequest,
            actionDispatcher,
            ct);
    }
}
