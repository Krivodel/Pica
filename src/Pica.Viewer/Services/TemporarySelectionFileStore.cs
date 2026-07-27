using Microsoft.Extensions.Logging;

namespace Pica.Viewer.Services;

internal sealed class TemporarySelectionFileStore : IDisposable
{
    private const string FilePrefix = "Pica-selection-";

    private readonly ILogger<TemporarySelectionFileStore> _logger;
    private readonly HashSet<string> _filePaths = [];
    private readonly object _sync = new();

    internal TemporarySelectionFileStore(ILogger<TemporarySelectionFileStore> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void Dispose()
    {
        string[] filePaths;

        lock (_sync)
        {
            filePaths = _filePaths.ToArray();
            _filePaths.Clear();
        }

        foreach (string filePath in filePaths)
        {
            try
            {
                File.Delete(filePath);
            }
            catch (IOException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to delete the Pica selection temporary file.");
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Access was denied while deleting the Pica selection temporary file.");
            }
        }
    }

    internal string CreateFilePath()
    {
        string fileName = $"{FilePrefix}{Guid.NewGuid():N}{PicaImageFormats.PngExtension}";

        return Path.Combine(Path.GetTempPath(), fileName);
    }

    internal async Task SaveAsync(
        string filePath,
        PreparedClipboardImage image,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(image);
        await File.WriteAllBytesAsync(filePath, image.PngContent, ct).ConfigureAwait(false);

        lock (_sync)
        {
            _filePaths.Add(filePath);
        }
    }
}
