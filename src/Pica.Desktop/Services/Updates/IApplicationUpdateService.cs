namespace Pica.Desktop.Services.Updates;

internal interface IApplicationUpdateService
{
    bool CanCheckForUpdates { get; }

    Task<PicaApplicationUpdate?> CheckForUpdateAsync(CancellationToken ct);

    Task DownloadUpdateAsync(
        PicaApplicationUpdate update,
        IProgress<int> progress,
        CancellationToken ct);

    void ApplyUpdateAndRestart(
        PicaApplicationUpdate update,
        IReadOnlyList<string> restartArguments);

    void ApplyUpdateAndExit(PicaApplicationUpdate update);
}
