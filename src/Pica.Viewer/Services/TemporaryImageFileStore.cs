using Microsoft.Extensions.Logging;

namespace Pica.Viewer.Services;

internal sealed class TemporaryImageFileStore : ITemporaryImageFileStore
{
    private const string ChannelFilePrefix = "Pica-channel-";
    private const string SelectionFilePrefix = "Pica-selection-";

    private readonly ILogger<TemporaryImageFileStore> _logger;
    private readonly HashSet<string> _filePaths = [];
    private readonly object _sync = new();
    private bool _disposed;

    internal TemporaryImageFileStore(ILogger<TemporaryImageFileStore> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string CreateChannelFilePath(ImageChannel channel)
    {
        ArgumentNullException.ThrowIfNull(channel);

        return CreateFilePath($"{ChannelFilePrefix}{channel.Code}-");
    }

    public string CreateSelectionFilePath()
    {
        return CreateFilePath(SelectionFilePrefix);
    }

    public async Task SaveAsync(
        string filePath,
        PreparedClipboardImage image,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(image);
        await File.WriteAllBytesAsync(filePath, image.PngContent, ct).ConfigureAwait(false);
        bool shouldDelete;

        lock (_sync)
        {
            shouldDelete = _disposed;

            if (!shouldDelete)
            {
                _filePaths.Add(filePath);
            }
        }

        if (shouldDelete)
        {
            DeleteFile(filePath);
        }
    }

    public void Dispose()
    {
        string[] filePaths;

        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            filePaths = _filePaths.ToArray();
            _filePaths.Clear();
        }

        foreach (string filePath in filePaths)
        {
            DeleteFile(filePath);
        }
    }

    private static string CreateFilePath(string prefix)
    {
        string fileName = $"{prefix}{Guid.NewGuid():N}{PicaImageFormats.PngExtension}";

        return Path.Combine(Path.GetTempPath(), fileName);
    }

    private void DeleteFile(string filePath)
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
