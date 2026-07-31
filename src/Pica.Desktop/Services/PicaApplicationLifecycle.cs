using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Microsoft.Extensions.Logging;

using Pica.Desktop.Services.Background;
using Pica.Desktop.Services.Updates;
using Pica.Desktop.Views;
using Pica.Viewer.Services;
using Pica.Viewer.Views;

namespace Pica.Desktop.Services;

internal sealed class PicaApplicationLifecycle : IDisposable
{
    private readonly CancellationTokenSource _applicationCancellationSource = new();
    private readonly PicaStartupRequestFactory _startupRequestFactory;
    private readonly PicaDesktopViewerWindowFactory _windowFactory;
    private readonly IPicaBackgroundIdleCoordinator _backgroundIdleCoordinator;
    private readonly PicaBackgroundIdlePreparation _backgroundIdlePreparation;
    private readonly IPicaDesktopStateService _desktopStateService;
    private readonly IClipboardImageWriter _clipboardImageWriter;
    private readonly ApplicationUpdateCoordinator _updateCoordinator;
    private readonly IApplicationUpdateService _updateService;
    private readonly ILogger<PicaApplicationLifecycle> _logger;
    private IClassicDesktopStyleApplicationLifetime? _desktopLifetime;
    private PicaHostConnection? _hostConnection;
    private PicaApplicationUpdate? _pendingUpdate;
    private IReadOnlyList<string> _updateRestartArguments =
        Array.Empty<string>();
    private bool _canWaitInBackground;
    private bool _isDisposed;
    private bool _isShutdownRequested;
    private bool _isShutdownStarted;
    private bool _shouldRestartAfterUpdate;

    public PicaApplicationLifecycle(
        PicaStartupRequestFactory startupRequestFactory,
        PicaDesktopViewerWindowFactory windowFactory,
        IPicaBackgroundIdleCoordinator backgroundIdleCoordinator,
        PicaBackgroundIdlePreparation backgroundIdlePreparation,
        IPicaDesktopStateService desktopStateService,
        IClipboardImageWriter clipboardImageWriter,
        ApplicationUpdateCoordinator updateCoordinator,
        IApplicationUpdateService updateService,
        ILogger<PicaApplicationLifecycle> logger)
    {
        _startupRequestFactory = startupRequestFactory
            ?? throw new ArgumentNullException(nameof(startupRequestFactory));
        _windowFactory = windowFactory
            ?? throw new ArgumentNullException(nameof(windowFactory));
        _backgroundIdleCoordinator = backgroundIdleCoordinator
            ?? throw new ArgumentNullException(nameof(backgroundIdleCoordinator));
        _backgroundIdlePreparation = backgroundIdlePreparation
            ?? throw new ArgumentNullException(nameof(backgroundIdlePreparation));
        _desktopStateService = desktopStateService
            ?? throw new ArgumentNullException(nameof(desktopStateService));
        _clipboardImageWriter = clipboardImageWriter
            ?? throw new ArgumentNullException(nameof(clipboardImageWriter));
        _updateCoordinator = updateCoordinator
            ?? throw new ArgumentNullException(nameof(updateCoordinator));
        _updateService = updateService
            ?? throw new ArgumentNullException(nameof(updateService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task StartAsync(
        IClassicDesktopStyleApplicationLifetime desktopLifetime,
        long? sourceWindowHandle,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(desktopLifetime);
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        ct.ThrowIfCancellationRequested();
        _desktopLifetime = desktopLifetime;
        desktopLifetime.ShutdownRequested += OnShutdownRequested;
        using CancellationTokenSource startupCancellationSource =
            CancellationTokenSource.CreateLinkedTokenSource(
                _applicationCancellationSource.Token,
                ct);

        try
        {
            string[] arguments = desktopLifetime.Args ?? [];
            PicaStartupRequest startupRequest = await _startupRequestFactory
                .CreateAsync(
                    arguments,
                    sourceWindowHandle,
                    startupCancellationSource.Token);
            await OpenViewerWindowAsync(
                desktopLifetime,
                startupRequest,
                arguments,
                startupCancellationSource.Token);
        }
        catch (OperationCanceledException)
            when (startupCancellationSource.IsCancellationRequested)
        {
            await ShutdownAsync(desktopLifetime);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pica failed to initialize");
            _canWaitInBackground = false;
            StartupErrorWindow errorWindow = new();
            ShowMainWindow(desktopLifetime, errorWindow);
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _backgroundIdleCoordinator.Cancel();
        _updateCoordinator.StopMonitoring();
        _applicationCancellationSource.Cancel();

        if (_desktopLifetime is not null)
        {
            _desktopLifetime.ShutdownRequested -= OnShutdownRequested;
            _desktopLifetime = null;
        }

        _applicationCancellationSource.Dispose();
        _isDisposed = true;
    }

    private async Task OpenViewerWindowAsync(
        IClassicDesktopStyleApplicationLifetime desktopLifetime,
        PicaStartupRequest startupRequest,
        IReadOnlyList<string> restartArguments,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(startupRequest);
        ArgumentNullException.ThrowIfNull(restartArguments);
        _hostConnection = startupRequest.HostConnection;
        ImageViewerWindow window = await _windowFactory.CreateAsync(
            startupRequest,
            ct);
        _shouldRestartAfterUpdate = _hostConnection is null;
        _updateRestartArguments = _shouldRestartAfterUpdate
            ? restartArguments.ToArray()
            : Array.Empty<string>();
        _canWaitInBackground = _hostConnection is null;
        ShowMainWindow(desktopLifetime, window);
        _updateCoordinator.StartMonitoring(
            window,
            update => RequestUpdateRestartAsync(
                desktopLifetime,
                update));

        _logger.LogInformation(
            "Pica viewer window opened with {ItemCount} images",
            startupRequest.ViewerRequest.Items.Count);
    }

    private void ShowMainWindow(
        IClassicDesktopStyleApplicationLifetime desktopLifetime,
        Window window)
    {
        window.Closed += OnMainWindowClosed;
        desktopLifetime.MainWindow = window;
        window.Show();
    }

    private Task RequestUpdateRestartAsync(
        IClassicDesktopStyleApplicationLifetime desktopLifetime,
        PicaApplicationUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);
        _pendingUpdate = update;
        Window? mainWindow = desktopLifetime.MainWindow;

        if (mainWindow is null)
        {
            throw new InvalidOperationException(
                "Pica main window is unavailable for an update restart.");
        }

        if (mainWindow is ImageViewerWindow viewerWindow)
        {
            viewerWindow.CloseForApplicationExit();
        }
        else
        {
            mainWindow.Close();
        }

        return Task.CompletedTask;
    }

    private async Task HandleMainWindowClosedAsync(
        IClassicDesktopStyleApplicationLifetime desktopLifetime,
        Task closeCleanupCompletion)
    {
        ArgumentNullException.ThrowIfNull(closeCleanupCompletion);

        try
        {
            if (CanWaitInBackground())
            {
                bool wasReactivated = await WaitForBackgroundActivationAsync(
                    desktopLifetime,
                    closeCleanupCompletion);

                if (wasReactivated)
                {
                    return;
                }
            }
            else
            {
                await closeCleanupCompletion;
                await _clipboardImageWriter.FlushAsync(
                    CancellationToken.None);
            }
        }
        catch (OperationCanceledException)
            when (_applicationCancellationSource.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Pica failed while completing window cleanup or waiting for a background activation");
        }

        await ShutdownAsync(desktopLifetime);
    }

    private bool CanWaitInBackground()
    {
        return _canWaitInBackground
            && (_hostConnection is null)
            && (_pendingUpdate is null)
            && !_isShutdownRequested;
    }

    private async Task<bool> WaitForBackgroundActivationAsync(
        IClassicDesktopStyleApplicationLifetime desktopLifetime,
        Task closeCleanupCompletion)
    {
        ArgumentNullException.ThrowIfNull(closeCleanupCompletion);
        PicaDesktopState state = await _desktopStateService.LoadAsync(
            _applicationCancellationSource.Token);
        TimeSpan idleTimeout = TimeSpan.FromSeconds(
            state.BackgroundIdleTimeoutSeconds);

        if (idleTimeout == TimeSpan.Zero)
        {
            await closeCleanupCompletion;
            await _clipboardImageWriter.FlushAsync(CancellationToken.None);
            return false;
        }

        _backgroundIdleCoordinator.Start(
            idleTimeout,
            _applicationCancellationSource.Token);
        _logger.LogInformation(
            "Pica is waiting in the background for {IdleTimeoutSeconds} seconds",
            state.BackgroundIdleTimeoutSeconds);
        IPicaBackgroundActivation? activation = null;
        Task<IPicaBackgroundActivation?> activationCompletion =
            _backgroundIdleCoordinator.Completion;

        try
        {
            await _backgroundIdlePreparation.PrepareAsync(
                closeCleanupCompletion,
                activationCompletion,
                _applicationCancellationSource.Token);
            await _clipboardImageWriter.FlushAsync(CancellationToken.None);
            activation = await activationCompletion;
        }
        finally
        {
            await _backgroundIdleCoordinator.StopAsync(
                CancellationToken.None);

            if ((activation is null)
                && activationCompletion.IsCompletedSuccessfully)
            {
                IPicaBackgroundActivation? unclaimedActivation =
                    await activationCompletion;

                if (unclaimedActivation is not null)
                {
                    await unclaimedActivation.DisposeAsync();
                }
            }
        }

        if (activation is null)
        {
            _logger.LogInformation(
                "Pica background idle timeout elapsed without a new activation");
            return false;
        }

        await using (activation)
        {
            PicaStartupRequest startupRequest = await _startupRequestFactory
                .CreateAsync(
                    activation.Arguments.ToArray(),
                    activation.SourceWindowHandle,
                    _applicationCancellationSource.Token);

            if (startupRequest.HostConnection is not null)
            {
                await startupRequest.HostConnection.DisposeAsync();
                throw new InvalidOperationException(
                    "A hosted Pica session cannot be opened through background activation.");
            }

            await OpenViewerWindowAsync(
                desktopLifetime,
                startupRequest,
                activation.Arguments,
                _applicationCancellationSource.Token);
            await activation.AcknowledgeAsync(
                _applicationCancellationSource.Token);
        }

        _logger.LogInformation(
            "Pica opened a new viewer window from a background activation");
        return true;
    }

    private async Task ShutdownAsync(
        IClassicDesktopStyleApplicationLifetime desktopLifetime)
    {
        if (_isShutdownStarted)
        {
            return;
        }

        _isShutdownStarted = true;
        _logger.LogInformation("Pica is starting graceful shutdown");
        _applicationCancellationSource.Cancel();
        _backgroundIdleCoordinator.Cancel();
        _updateCoordinator.StopMonitoring();

        try
        {
            await _clipboardImageWriter.FlushAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Pica could not flush the clipboard during shutdown");
        }

        if (_hostConnection is not null)
        {
            await _hostConnection.DisposeAsync();
            _hostConnection = null;
        }

        ApplyPendingUpdate();
        desktopLifetime.ShutdownRequested -= OnShutdownRequested;
        _logger.LogInformation("Pica graceful shutdown completed");
        desktopLifetime.Shutdown();
    }

    private void ApplyPendingUpdate()
    {
        if (_pendingUpdate is null)
        {
            return;
        }

        try
        {
            if (_shouldRestartAfterUpdate)
            {
                _updateService.ApplyUpdateAndRestart(
                    _pendingUpdate,
                    _updateRestartArguments);
            }
            else
            {
                _updateService.ApplyUpdateAndExit(_pendingUpdate);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Pica update {UpdateVersion} could not be applied after shutdown.",
                _pendingUpdate.Version);
        }
    }

    private async void OnMainWindowClosed(object? sender, EventArgs e)
    {
        if (_isShutdownStarted)
        {
            return;
        }

        if (sender is Window closedWindow)
        {
            closedWindow.Closed -= OnMainWindowClosed;
        }

        Task closeCleanupCompletion = sender is ImageViewerWindow viewerWindow
            ? viewerWindow.CloseCleanupCompletion
            : Task.CompletedTask;
        _ = e;
        _logger.LogInformation("Pica main window closed");
        _updateCoordinator.StopMonitoring();

        if (_desktopLifetime is not { } desktopLifetime)
        {
            return;
        }

        desktopLifetime.MainWindow = null;
        await HandleMainWindowClosedAsync(
            desktopLifetime,
            closeCleanupCompletion);
    }

    private async void OnShutdownRequested(
        object? sender,
        ShutdownRequestedEventArgs e)
    {
        _ = sender;
        _ = e;
        _isShutdownRequested = true;
        _backgroundIdleCoordinator.Cancel();

        if ((_desktopLifetime is { } desktopLifetime)
            && (desktopLifetime.MainWindow is null))
        {
            await ShutdownAsync(desktopLifetime);
        }
    }
}
