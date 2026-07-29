using Microsoft.Extensions.Logging;

namespace Pica.Viewer.Services;

internal sealed class ImageFileMetadataProvider :
    IImageFileMetadataProvider
{
    private readonly ILogger<ImageFileMetadataProvider> _logger;

    public ImageFileMetadataProvider(
        ILogger<ImageFileMetadataProvider> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<DateTime?> GetModificationDateAsync(
        string filePath,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        return Task.Run(
            () => GetModificationDate(filePath, ct),
            ct);
    }

    private DateTime? GetModificationDate(
        string filePath,
        CancellationToken ct)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            DateTime? modificationDate = File.Exists(filePath)
                ? File.GetLastWriteTime(filePath)
                : null;
            ct.ThrowIfCancellationRequested();

            return modificationDate;
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or NotSupportedException)
        {
            _logger.LogWarning(
                ex,
                "Failed to read the Pica image modification date.");

            return null;
        }
    }
}
