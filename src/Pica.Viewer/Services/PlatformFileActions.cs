namespace Pica.Viewer.Services;

internal abstract class PlatformFileActions : IPlatformFileActions
{
    public abstract bool SupportsOpenWith { get; }

    public IReadOnlyList<OpenWithApplication> GetOpenWithApplications(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        return GetOpenWithApplicationsCore(filePath);
    }

    public Task RevealInFolderAsync(
        string filePath,
        FileRevealWindowMode windowMode,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ct.ThrowIfCancellationRequested();

        if (!Enum.IsDefined(windowMode))
        {
            throw new ArgumentOutOfRangeException(
                nameof(windowMode),
                windowMode,
                "Unsupported file reveal window mode.");
        }

        return RevealInFolderCoreAsync(filePath, windowMode, ct);
    }

    public Task OpenWithAsync(
        string filePath,
        OpenWithApplication application,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(application);
        ct.ThrowIfCancellationRequested();

        return OpenWithCoreAsync(filePath, application, ct);
    }

    public Task ChooseApplicationAsync(string filePath, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ct.ThrowIfCancellationRequested();

        return ChooseApplicationCoreAsync(filePath, ct);
    }

    protected abstract IReadOnlyList<OpenWithApplication> GetOpenWithApplicationsCore(
        string filePath);

    protected abstract Task RevealInFolderCoreAsync(
        string filePath,
        FileRevealWindowMode windowMode,
        CancellationToken ct);

    protected abstract Task OpenWithCoreAsync(
        string filePath,
        OpenWithApplication application,
        CancellationToken ct);

    protected abstract Task ChooseApplicationCoreAsync(
        string filePath,
        CancellationToken ct);
}
