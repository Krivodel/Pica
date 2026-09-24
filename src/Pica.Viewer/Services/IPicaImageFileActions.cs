using Avalonia.Platform.Storage;

namespace Pica.Viewer.Services;

public interface IPicaImageFileActions
{
    bool SupportsOpenWith { get; }

    void Attach(IStorageProvider storageProvider);

    Task<bool> SaveAsAsync(string filePath, CancellationToken ct);

    Task<IReadOnlyList<OpenWithApplication>> GetOpenWithApplicationsAsync(
        string filePath,
        CancellationToken ct);

    Task OpenWithAsync(
        string filePath,
        OpenWithApplication application,
        CancellationToken ct);

    Task ChooseApplicationAsync(string filePath, CancellationToken ct);
}
