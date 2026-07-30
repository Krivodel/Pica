using Avalonia.Threading;
using Microsoft.Extensions.Logging;

using SukiUI.Controls;

using Pica.Desktop.Views.Updates;

namespace Pica.Desktop.Services.Updates;

internal sealed class ApplicationUpdateCoordinator : IDisposable
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(30d);

    private readonly IApplicationUpdateService _updateService;
    private readonly ApplicationUpdateToastPresenter _presenter;
    private readonly ILogger<ApplicationUpdateCoordinator> _logger;
    private CancellationTokenSource? _monitoringCancellationSource;
    private Func<PicaApplicationUpdate, Task>? _restartApplication;
    private PicaApplicationUpdate? _presentedUpdate;
    private SukiWindow? _owner;
    private string? _dismissedVersion;
    private bool _isDisposed;
    private bool _isMonitoring;
    private int _monitoringGeneration;

    public ApplicationUpdateCoordinator(
        IApplicationUpdateService updateService,
        ApplicationUpdateToastPresenter presenter,
        ILogger<ApplicationUpdateCoordinator> logger)
    {
        _updateService = updateService
            ?? throw new ArgumentNullException(nameof(updateService));
        _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _presenter.UpdateRequested += OnUpdateRequested;
        _presenter.LaterRequested += OnLaterRequested;
    }

    public void StartMonitoring(
        SukiWindow owner,
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
        _monitoringGeneration++;
        _monitoringCancellationSource = new CancellationTokenSource();
        _owner = owner;
        _restartApplication = restartApplication;
        _presenter.Attach(owner);
        _ = MonitorAsync(
            _monitoringGeneration,
            _monitoringCancellationSource.Token);
    }

    public void StopMonitoring()
    {
        if (_isDisposed)
        {
            return;
        }

        _monitoringCancellationSource?.Cancel();
        _monitoringCancellationSource?.Dispose();
        _monitoringCancellationSource = null;
        _owner = null;
        _restartApplication = null;
        _presentedUpdate = null;
        _dismissedVersion = null;
        _isMonitoring = false;
        _presenter.Detach();
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        StopMonitoring();
        _presenter.UpdateRequested -= OnUpdateRequested;
        _presenter.LaterRequested -= OnLaterRequested;
        _isDisposed = true;
    }

    private async Task MonitorAsync(
        int monitoringGeneration,
        CancellationToken ct)
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
            await CheckAndPresentUpdateAsync(
                monitoringGeneration,
                ct).ConfigureAwait(false);

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

    private async Task CheckAndPresentUpdateAsync(
        int monitoringGeneration,
        CancellationToken ct)
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
                () => ShowUpdate(update, monitoringGeneration),
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

    private void ShowUpdate(
        PicaApplicationUpdate update,
        int monitoringGeneration)
    {
        if ((monitoringGeneration != _monitoringGeneration)
            || !_isMonitoring
            || (_owner is null)
            || !_owner.IsVisible
            || (_presentedUpdate is not null))
        {
            return;
        }

        _presentedUpdate = update;
        _presenter.ShowAvailable(update.Version);
    }

    private async Task InstallUpdateAsync(PicaApplicationUpdate update)
    {
        CancellationTokenSource monitoringCancellationSource =
            _monitoringCancellationSource
            ?? throw new InvalidOperationException(
                "Pica update monitoring has not been started.");

        try
        {
            _presenter.ShowDownloadProgress(0);
            Progress<int> progress = new(_presenter.ShowDownloadProgress);
            await _updateService
                .DownloadUpdateAsync(
                    update,
                    progress,
                    monitoringCancellationSource.Token);
            _presenter.ShowInstalling();

            Func<PicaApplicationUpdate, Task> restartApplication =
                _restartApplication
                ?? throw new InvalidOperationException(
                    "Pica update restart callback has not been configured.");
            await restartApplication(update);
        }
        catch (OperationCanceledException)
            when (monitoringCancellationSource.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Pica update {UpdateVersion} could not be installed.",
                update.Version);
            _presenter.ShowInstallFailure();
        }
    }

    private void OnUpdateRequested(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        if (_presentedUpdate is { } update)
        {
            _ = InstallUpdateAsync(update);
        }
    }

    private void OnLaterRequested(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        if (_presentedUpdate is not { } update)
        {
            return;
        }

        _dismissedVersion = update.Version;
        _presentedUpdate = null;
        _presenter.Dismiss();
    }
}
