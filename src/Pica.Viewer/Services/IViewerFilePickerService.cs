using Avalonia.Platform.Storage;

namespace Pica.Viewer.Services;

internal interface IViewerFilePickerService
{
    Task<IStorageFile?> GetFileFromPathAsync(
        string filePath,
        CancellationToken ct);

    Task<IStorageFile?> SelectSaveDestinationAsync(
        FilePickerSaveOptions options,
        CancellationToken ct);
}
