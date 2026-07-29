using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.TestDoubles;

internal sealed class NullPlatformFileActions : IPlatformFileActions
{
    public bool SupportsOpenWith => false;

    public Task<IReadOnlyList<OpenWithApplication>>
        GetOpenWithApplicationsAsync(
            string filePath,
            CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ct.ThrowIfCancellationRequested();
        IReadOnlyList<OpenWithApplication> applications =
            new List<OpenWithApplication>();

        return Task.FromResult(applications);
    }

    public Task RevealInFolderAsync(
        string filePath,
        FileRevealWindowMode windowMode,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _ = windowMode;
        ct.ThrowIfCancellationRequested();

        return Task.CompletedTask;
    }

    public Task OpenWithAsync(
        string filePath,
        OpenWithApplication application,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(application);
        ct.ThrowIfCancellationRequested();

        return Task.CompletedTask;
    }

    public Task ChooseApplicationAsync(
        string filePath,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ct.ThrowIfCancellationRequested();

        return Task.CompletedTask;
    }
}
