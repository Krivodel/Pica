using Microsoft.Extensions.Logging;

using Pica.Viewer.ViewModels;
using Pica.Viewer.Views;

namespace Pica.Viewer.Services;

internal sealed class ImageViewerWindowLifetime : IDisposable
{
    private readonly ImageViewerWindow _window;
    private readonly AvaloniaUiFrameScheduler _frameScheduler;
    private readonly ImageViewerSessionViewModel _session;
    private readonly ImageViewerPresentationServices _presentationServices;
    private readonly ImageViewerSettingsServices _settingsServices;
    private readonly ImageViewerInteractionServices _interactionServices;
    private readonly ILogger<ImageViewerWindowLifetime> _logger;
    private bool _isStarted;
    private bool _isClosed;

    internal ImageViewerWindowLifetime(
        ImageViewerWindow window,
        AvaloniaUiFrameScheduler frameScheduler,
        ImageViewerSessionViewModel session,
        ImageViewerPresentationServices presentationServices,
        ImageViewerSettingsServices settingsServices,
        ImageViewerInteractionServices interactionServices,
        ILogger<ImageViewerWindowLifetime> logger)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _frameScheduler = frameScheduler
            ?? throw new ArgumentNullException(nameof(frameScheduler));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _presentationServices = presentationServices
            ?? throw new ArgumentNullException(nameof(presentationServices));
        _settingsServices = settingsServices
            ?? throw new ArgumentNullException(nameof(settingsServices));
        _interactionServices = interactionServices
            ?? throw new ArgumentNullException(nameof(interactionServices));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _window.ReadyForLoading += OnWindowReadyForLoading;
        _window.Closed += OnWindowClosed;
    }

    public void Dispose()
    {
        if (_isClosed)
        {
            return;
        }

        _isClosed = true;
        DetachWindowEvents();
        _interactionServices.DisposeWithoutFlush();
        DisposeSharedServices();
    }

    private void DetachWindowEvents()
    {
        _window.ReadyForLoading -= OnWindowReadyForLoading;
        _window.Closed -= OnWindowClosed;
    }

    private void DisposeSharedServices()
    {
        _settingsServices.Dispose();
        _presentationServices.Dispose();
        _session.Dispose();
        _frameScheduler.Dispose();
    }

    private async Task FlushInteractionAndDisposeAsync()
    {
        try
        {
            await _interactionServices
                .FlushAndDisposeAsync(CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to flush the Pica clipboard after closing the viewer.");
        }
    }

    private void OnWindowReadyForLoading(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        if (_isStarted)
        {
            return;
        }

        _isStarted = true;
        _logger.LogInformation(
            "Pica viewer opened with {ItemCount} images in {WindowMode} mode",
            _session.Items.Count,
            _window.CurrentWindowMode);
        _presentationServices.LoadCoordinator.Start();
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        if (_isClosed)
        {
            return;
        }

        _isClosed = true;
        DetachWindowEvents();
        _interactionServices.DisposeViewModels();
        DisposeSharedServices();
        _logger.LogInformation("Pica viewer closed");
        _ = FlushInteractionAndDisposeAsync();
    }
}
