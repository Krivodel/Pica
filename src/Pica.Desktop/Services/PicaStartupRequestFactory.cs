using Microsoft.Extensions.Logging;

using Pica.Protocol;
using Pica.Viewer.Services;

namespace Pica.Desktop.Services;

public sealed class PicaStartupRequestFactory
{
    private readonly IImageFormatRegistry _formatRegistry;
    private readonly ILogger<PicaStartupRequestFactory> _logger;

    public PicaStartupRequestFactory(
        IImageFormatRegistry formatRegistry,
        ILogger<PicaStartupRequestFactory> logger)
    {
        _formatRegistry = formatRegistry ?? throw new ArgumentNullException(nameof(formatRegistry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PicaStartupRequest> CreateAsync(string[] arguments, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if ((arguments.Length == 2)
            && string.Equals(arguments[0], PicaProtocolConstants.PipeArgument, StringComparison.Ordinal))
        {
            _logger.LogInformation("Creating a hosted Pica viewer session");
            PicaHostConnection connection = await PicaHostConnection
                .ConnectAsync(arguments[1], ct)
                .ConfigureAwait(false);
            PicaViewerRequest request = await connection
                .ReceiveRequestAsync(ct)
                .ConfigureAwait(false);
            _logger.LogInformation(
                "Received hosted Pica request with {ItemCount} images and {ActionCount} actions",
                request.Items.Count,
                request.Actions.Count);

            return new PicaStartupRequest(request, connection);
        }

        List<string> requestedPaths = arguments
            .Where(File.Exists)
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        string? selectedPath = requestedPaths.FirstOrDefault();
        IReadOnlyList<string> imagePaths = ArePathsFromSameDirectory(requestedPaths)
            ? GetDirectoryImagePaths(requestedPaths[0])
            : requestedPaths;
        List<PicaImageItem> items = imagePaths
            .Select(CreateImageItem)
            .ToList();
        Guid selectedItemId = selectedPath is null
            ? Guid.Empty
            : CreateStableItemId(selectedPath);
        PicaViewerRequest viewerRequest = new(
            items,
            selectedItemId,
            new List<PicaActionDefinition>(),
            null);
        _logger.LogInformation(
            "Created standalone Pica request from {ArgumentCount} arguments with {ItemCount} supported images",
            arguments.Length,
            items.Count);

        return new PicaStartupRequest(viewerRequest, null);
    }

    private static bool ArePathsFromSameDirectory(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0)
        {
            return false;
        }

        string? selectedDirectoryPath = Path.GetDirectoryName(paths[0]);

        return paths.All(path => string.Equals(
            Path.GetDirectoryName(path),
            selectedDirectoryPath,
            StringComparison.OrdinalIgnoreCase));
    }

    private IReadOnlyList<string> GetDirectoryImagePaths(string selectedPath)
    {
        string? directoryPath = Path.GetDirectoryName(selectedPath);

        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return new List<string> { selectedPath };
        }

        try
        {
            List<string> imagePaths = Directory
                .EnumerateFiles(directoryPath, "*", SearchOption.TopDirectoryOnly)
                .Where(_formatRegistry.IsSupportedFileName)
                .Select(Path.GetFullPath)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .ThenBy(path => Path.GetFileName(path), StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            return imagePaths.Contains(selectedPath, StringComparer.OrdinalIgnoreCase)
                ? imagePaths
                : [selectedPath];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(
                ex,
                "Failed to enumerate adjacent images; using only the selected image");

            return new List<string> { selectedPath };
        }
    }

    private static PicaImageItem CreateImageItem(string path)
    {
        return new PicaImageItem(CreateStableItemId(path), path, Path.GetFileName(path));
    }

    private static Guid CreateStableItemId(string path)
    {
        byte[] hash = System.Security.Cryptography.MD5.HashData(
            System.Text.Encoding.UTF8.GetBytes(path.ToUpperInvariant()));

        return new Guid(hash);
    }
}
