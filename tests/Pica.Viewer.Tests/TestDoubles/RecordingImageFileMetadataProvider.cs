using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.TestDoubles;

internal sealed class RecordingImageFileMetadataProvider :
    IImageFileMetadataProvider
{
    internal DateTime? ModificationDate { get; set; }
    internal int CallCount { get; private set; }
    internal Exception? ExceptionToThrow { get; set; }

    public Task<DateTime?> GetModificationDateAsync(
        string filePath,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ct.ThrowIfCancellationRequested();
        CallCount++;

        if (ExceptionToThrow is not null)
        {
            return Task.FromException<DateTime?>(
                ExceptionToThrow);
        }

        return Task.FromResult(ModificationDate);
    }
}
