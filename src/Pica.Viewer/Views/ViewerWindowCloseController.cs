using Microsoft.Extensions.Logging;

using Avalonia.Controls;

using Pica.Viewer.ViewModels;

namespace Pica.Viewer.Views;

internal sealed class ViewerWindowCloseController
{
    private readonly ImageViewerWindow _window;
    private readonly ImageViewerSettingsViewModel _settings;
    private readonly ViewerWindowModeController _windowMode;
    private readonly ILogger<ImageViewerWindow> _logger;
    private bool _isSavingState;
    private bool _isClosingAfterStateSave;

    internal ViewerWindowCloseController(
        ImageViewerWindow window,
        ImageViewerSettingsViewModel settings,
        ViewerWindowModeController windowMode,
        ILogger<ImageViewerWindow> logger)
    {
        _window = window
            ?? throw new ArgumentNullException(nameof(window));
        _settings = settings
            ?? throw new ArgumentNullException(nameof(settings));
        _windowMode = windowMode
            ?? throw new ArgumentNullException(nameof(windowMode));
        _logger = logger
            ?? throw new ArgumentNullException(nameof(logger));
    }

    internal async Task HandleClosingAsync(
        WindowClosingEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        if (_isClosingAfterStateSave || e.Cancel)
        {
            return;
        }

        e.Cancel = true;

        if (_isSavingState)
        {
            return;
        }

        _isSavingState = true;

        try
        {
            await PersistStateAsync(CancellationToken.None);

            if (!_settings.HasErrorMessage)
            {
                _logger.LogDebug(
                    "Persisted Pica viewer state before closing");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to save the Pica window position before closing.");
        }

        _isClosingAfterStateSave = true;
        _window.Close();
    }

    private async Task PersistStateAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        _windowMode.CapturePlacement();
        _windowMode.UpdatePlacement();
        await _settings.PersistWindowStateCommand.ExecuteAsync(null);
    }
}
