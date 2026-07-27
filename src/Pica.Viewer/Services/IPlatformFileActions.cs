namespace Pica.Viewer.Services;

internal interface IPlatformFileActions
{
    bool SupportsOpenWith { get; }

    IReadOnlyList<OpenWithApplication> GetOpenWithApplications(string filePath);

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
