using Avalonia.Controls;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;

using Pica.Desktop.Views;

namespace Pica.Desktop.Services.Updates;

internal sealed class ApplicationUpdateCoordinator : IDisposable
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(30d);

    private readonly IApplicationUpdateService _updateService;
    private readonly ILogger<ApplicationUpdateCoordinator> _logger;
    private readonly CancellationTokenSource _lifetimeCancellationSource = new();
    private Func<PicaApplicationUpdate, Task>? _restartApplication;
    private ApplicationUpdateWindow? _window;
    private Window? _owner;
    private string? _dismissedVersion;
    private bool _isDisposed;
    private bool _isMonitoring;

    public ApplicationUpdateCoordinator(
        IApplicationUpdateService updateService,
        ILogger<ApplicationUpdateCoordinator> logger)
    {
        _updateService = updateService
            ?? throw new ArgumentNullException(nameof(updateService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void StartMonitoring(
        Window owner,
        Func<PicaApplicationUpdate, Task> restartApplication)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(restartApplication);
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (_isMonitoring)
        {
            throw new InvalidOperationException(
                "Pica update monitoring has already been started.");
        }

        _isMonitoring = true;
        _owner = owner;
        _restartApplication = restartApplication;
        _ = MonitorAsync(_lifetimeCancellationSource.Token);
    }

    public void StopMonitoring()
    {
        if (_isDisposed)
        {
            return;
        }

        _lifetimeCancellationSource.Cancel();
        _owner = null;
        _restartApplication = null;
        CloseWindow();
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        StopMonitoring();
        _isDisposed = true;
        _lifetimeCancellationSource.Dispose();
    }

    private async Task MonitorAsync(CancellationToken ct)
    {
        try
        {
            if (!_updateService.CanCheckForUpdates)
            {
                return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Pica update monitoring could not be initialized.");
            return;
        }

        while (!ct.IsCancellationRequested)
        {
            await CheckAndPresentUpdateAsync(ct).ConfigureAwait(false);

            try
            {
                await Task.Delay(CheckInterval, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
        }
    }

    private async Task CheckAndPresentUpdateAsync(CancellationToken ct)
    {
        try
        {
            PicaApplicationUpdate? update = await _updateService
                .CheckForUpdateAsync(ct)
                .ConfigureAwait(false);

            if ((update is null)
                || string.Equals(
                    update.Version,
                    _dismissedVersion,
                    StringComparison.Ordinal))
            {
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(
                () => ShowUpdate(update),
                DispatcherPriority.Normal,
                ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Pica update check failed.");
        }
    }

    private void ShowUpdate(PicaApplicationUpdate update)
    {
        if ((_owner is null)
            || !_owner.IsVisible
            || (_window is not null))
        {
            return;
        }

        ApplicationUpdateWindow window = new(update.Version);
        window.UpdateRequested += (_, _) => _ = InstallUpdateAsync(window, update);
        window.LaterRequested += (_, _) =>
        {
            _dismissedVersion = update.Version;
            window.Close();
        };
        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(_window, window))
            {
                _window = null;
            }
        };
        _window = window;
        window.Show(_owner);
    }

    private async Task InstallUpdateAsync(
        ApplicationUpdateWindow window,
        PicaApplicationUpdate update)
    {
        try
        {
            window.ShowDownloadProgress(0);
            Progress<int> progress = new(window.ShowDownloadProgress);
            await _updateService
                .DownloadUpdateAsync(
                    update,
                    progress,
                    _lifetimeCancellationSource.Token);
            window.ShowInstalling();

            Func<PicaApplicationUpdate, Task> restartApplication =
                _restartApplication
                ?? throw new InvalidOperationException(
                    "Pica update restart callback has not been configured.");
            await restartApplication(update);
        }
        catch (OperationCanceledException)
            when (_lifetimeCancellationSource.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Pica update {UpdateVersion} could not be installed.",
                update.Version);
            window.ShowInstallFailure();
        }
    }

    private void CloseWindow()
    {
        if (_window is null)
        {
            return;
        }

        ApplicationUpdateWindow window = _window;
        _window = null;

        if (Dispatcher.UIThread.CheckAccess())
        {
            window.CloseForShutdown();
            return;
        }

        Dispatcher.UIThread.Post(window.CloseForShutdown);
    }
}
