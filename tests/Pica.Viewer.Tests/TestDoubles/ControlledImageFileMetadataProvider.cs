using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.TestDoubles;

internal sealed class ControlledImageFileMetadataProvider :
    IImageFileMetadataProvider
{
    internal IReadOnlyList<string> RequestedFilePaths =>
        _requestedFilePaths;

    private readonly List<string> _requestedFilePaths = [];
    private readonly Dictionary<
        string,
        TaskCompletionSource<DateTime?>> _requests =
        new(StringComparer.OrdinalIgnoreCase);

    public Task<DateTime?> GetModificationDateAsync(
        string filePath,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ct.ThrowIfCancellationRequested();
        TaskCompletionSource<DateTime?> request = new();
        _requestedFilePaths.Add(filePath);
        _requests.Add(filePath, request);

        return request.Task;
    }

    internal void Complete(string filePath, DateTime? modificationDate)
    {
        _requests[filePath].SetResult(modificationDate);
    }
}
