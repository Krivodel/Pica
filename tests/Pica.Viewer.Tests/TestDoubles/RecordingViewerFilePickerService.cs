using Avalonia.Platform.Storage;

using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.TestDoubles;

internal sealed class RecordingViewerFilePickerService : IViewerFilePickerService
{
    internal IStorageFile? SourceFile { get; set; }
    internal string? RequestedFilePath { get; private set; }

    public Task<IStorageFile?> GetFileFromPathAsync(
        string filePath,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ct.ThrowIfCancellationRequested();
        RequestedFilePath = filePath;

        return Task.FromResult(SourceFile);
    }

    public Task<IStorageFile?> SelectSaveDestinationAsync(
        FilePickerSaveOptions options,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(options);
        ct.ThrowIfCancellationRequested();

        throw new NotSupportedException(
            "This test file picker does not support save destinations.");
    }
}
