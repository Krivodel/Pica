using System.Reflection;

using Microsoft.Extensions.Logging;

using Velopack;
using Velopack.Sources;

namespace Pica.Desktop.Services.Updates;

internal sealed class VelopackApplicationUpdateService : IApplicationUpdateService
{
    public bool CanCheckForUpdates => GetUpdateManager().IsInstalled;

    private static readonly string RepositoryUrl = GetRepositoryUrl();
    private readonly ILogger<VelopackApplicationUpdateService> _logger;
    private readonly object _syncRoot = new();
    private UpdateManager? _updateManager;

    public VelopackApplicationUpdateService(
        ILogger<VelopackApplicationUpdateService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PicaApplicationUpdate?> CheckForUpdateAsync(CancellationToken ct)
    {
        if (!CanCheckForUpdates)
        {
            _logger.LogDebug(
                "Pica update check skipped because the application is not installed by Velopack.");
            return null;
        }

        _logger.LogInformation("Checking GitHub Releases for a Pica update.");
        UpdateInfo? updateInfo = await GetUpdateManager()
            .CheckForUpdatesAsync()
            .WaitAsync(ct)
            .ConfigureAwait(false);

        if (updateInfo is null)
        {
            _logger.LogInformation("No Pica update is available.");
            return null;
        }

        PicaApplicationUpdate update = new(updateInfo);
        _logger.LogInformation(
            "Pica update {UpdateVersion} is available.",
            update.Version);

        return update;
    }

    public async Task DownloadUpdateAsync(
        PicaApplicationUpdate update,
        IProgress<int> progress,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(update);
        ArgumentNullException.ThrowIfNull(progress);

        _logger.LogInformation(
            "Downloading Pica update {UpdateVersion}.",
            update.Version);
        await GetUpdateManager()
            .DownloadUpdatesAsync(
                update.NativeUpdate,
                progress.Report,
                ct)
            .ConfigureAwait(false);
        _logger.LogInformation(
            "Pica update {UpdateVersion} was downloaded.",
            update.Version);
    }

    public void ApplyUpdateAndRestart(
        PicaApplicationUpdate update,
        IReadOnlyList<string> restartArguments)
    {
        ArgumentNullException.ThrowIfNull(update);
        ArgumentNullException.ThrowIfNull(restartArguments);

        _logger.LogInformation(
            "Applying Pica update {UpdateVersion} and restarting.",
            update.Version);
        GetUpdateManager().ApplyUpdatesAndRestart(
            update.NativeUpdate.TargetFullRelease,
            restartArguments.ToArray());
    }

    public void ApplyUpdateAndExit(PicaApplicationUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);

        _logger.LogInformation(
            "Applying Pica update {UpdateVersion} without restarting a hosted session.",
            update.Version);
        GetUpdateManager().ApplyUpdatesAndExit(
            update.NativeUpdate.TargetFullRelease);
    }

    private static string GetRepositoryUrl()
    {
        AssemblyMetadataAttribute? repositoryMetadata =
            typeof(VelopackApplicationUpdateService)
                .Assembly
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .SingleOrDefault(attribute =>
                    string.Equals(
                        attribute.Key,
                        nameof(RepositoryUrl),
                        StringComparison.Ordinal));
        string repositoryUrl = repositoryMetadata?.Value
            ?? throw new InvalidOperationException(
                "Pica assembly metadata does not contain RepositoryUrl.");

        return repositoryUrl.EndsWith(
            ".git",
            StringComparison.OrdinalIgnoreCase)
            ? repositoryUrl[..^4]
            : repositoryUrl;
    }

    private UpdateManager GetUpdateManager()
    {
        lock (_syncRoot)
        {
            if (_updateManager is null)
            {
                GithubSource updateSource = new(
                    RepositoryUrl,
                    accessToken: null,
                    prerelease: false);
                _updateManager = new UpdateManager(updateSource);
            }

            return _updateManager;
        }
    }
}
