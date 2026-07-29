namespace Pica.Viewer.Services;

internal interface IPlatformFileActions
{
    bool SupportsOpenWith { get; }

    Task<IReadOnlyList<OpenWithApplication>> GetOpenWithApplicationsAsync(
        string filePath,
        CancellationToken ct);

    Task RevealInFolderAsync(
        string filePath,
        FileRevealWindowMode windowMode,
        CancellationToken ct);

    Task OpenWithAsync(
        string filePath,
        OpenWithApplication application,
        CancellationToken ct);

    Task ChooseApplicationAsync(string filePath, CancellationToken ct);
}
