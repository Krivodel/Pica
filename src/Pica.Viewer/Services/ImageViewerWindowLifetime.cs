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

    private async Task CompleteCloseCleanupAsync()
    {
        List<Exception> failures = [];
        ExecuteCleanup(
            _interactionServices.DisposeViewModels,
            "detach viewer view models",
            failures);
        ExecuteCleanup(
            _settingsServices.Dispose,
            "dispose viewer settings services",
            failures);
        await ExecuteCleanupAsync(
            () => _presentationServices.DisposeAsync(
                CancellationToken.None),
            "dispose viewer presentation services",
            failures).ConfigureAwait(false);
        ExecuteCleanup(
            _session.Dispose,
            "dispose the viewer session",
            failures);
        ExecuteCleanup(
            _frameScheduler.Dispose,
            "dispose the viewer frame scheduler",
            failures);
        await ExecuteCleanupAsync(
            () => _interactionServices.FlushAndDisposeAsync(
                CancellationToken.None),
            "flush and dispose viewer interaction services",
            failures).ConfigureAwait(false);
        Exception? visualCleanupException =
            _window.GetCloseVisualCleanupException();

        if (visualCleanupException is not null)
        {
            failures.Add(visualCleanupException);
            _logger.LogError(
                visualCleanupException,
                "Pica viewer visual resources were not fully detached.");
        }

        if (failures.Count > 0)
        {
            Exception cleanupException = failures.Count == 1
                ? failures[0]
                : new AggregateException(
                    "Pica viewer close cleanup failed.",
                    failures);
            _logger.LogError(
                cleanupException,
                "Pica viewer close cleanup did not complete successfully.");
            _window.FailCloseCleanup(cleanupException);
            return;
        }

        _logger.LogInformation("Pica viewer closed");
        _window.CompleteCloseCleanup();
    }

    private void ExecuteCleanup(
        Action cleanup,
        string operationName,
        ICollection<Exception> failures)
    {
        ArgumentNullException.ThrowIfNull(cleanup);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        ArgumentNullException.ThrowIfNull(failures);

        try
        {
            cleanup();
        }
        catch (Exception ex)
        {
            failures.Add(ex);
            _logger.LogError(
                ex,
                "Failed to {CleanupOperation} while closing the Pica viewer.",
                operationName);
        }
    }

    private async Task ExecuteCleanupAsync(
        Func<Task> cleanup,
        string operationName,
        ICollection<Exception> failures)
    {
        ArgumentNullException.ThrowIfNull(cleanup);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        ArgumentNullException.ThrowIfNull(failures);

        try
        {
            await cleanup().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            failures.Add(ex);
            _logger.LogError(
                ex,
                "Failed to {CleanupOperation} while closing the Pica viewer.",
                operationName);
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
        _ = CompleteCloseCleanupAsync();
    }
}
