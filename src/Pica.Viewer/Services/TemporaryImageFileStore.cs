using Microsoft.Extensions.Logging;

namespace Pica.Viewer.Services;

internal sealed class TemporaryImageFileStore : IDisposable
{
    private const string ChannelFilePrefix = "Pica-channel-";
    private const string SelectionFilePrefix = "Pica-selection-";

    private readonly ILogger<TemporaryImageFileStore> _logger;
    private readonly HashSet<string> _filePaths = [];
    private readonly object _sync = new();

    internal TemporaryImageFileStore(ILogger<TemporaryImageFileStore> logger)
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
                    "Failed to delete a temporary Pica image file.");
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Access was denied while deleting a temporary Pica image file.");
            }
        }
    }

    internal string CreateChannelFilePath(ImageChannel channel)
    {
        ArgumentNullException.ThrowIfNull(channel);

        return CreateFilePath($"{ChannelFilePrefix}{channel.Code}-");
    }

    internal string CreateSelectionFilePath()
    {
        return CreateFilePath(SelectionFilePrefix);
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

    private static string CreateFilePath(string prefix)
    {
        string fileName = $"{prefix}{Guid.NewGuid():N}{PicaImageFormats.PngExtension}";

        return Path.Combine(Path.GetTempPath(), fileName);
    }
}
