using Microsoft.Extensions.Logging;

using Pica.Protocol;
using Pica.Viewer.Services;

namespace Pica.Desktop.Services;

public sealed class PicaStartupRequestFactory
{
    private readonly IImageFormatRegistry _formatRegistry;
    private readonly IWindowsExplorerItemOrderProvider
        _explorerItemOrderProvider;
    private readonly ILogger<PicaStartupRequestFactory> _logger;

    public PicaStartupRequestFactory(
        IImageFormatRegistry formatRegistry,
        IWindowsExplorerItemOrderProvider explorerItemOrderProvider,
        ILogger<PicaStartupRequestFactory> logger)
    {
        _formatRegistry = formatRegistry ?? throw new ArgumentNullException(nameof(formatRegistry));
        _explorerItemOrderProvider = explorerItemOrderProvider
            ?? throw new ArgumentNullException(
                nameof(explorerItemOrderProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<PicaStartupRequest> CreateAsync(
        string[] arguments,
        CancellationToken ct)
    {
        return CreateAsync(arguments, null, ct);
    }

    public async Task<PicaStartupRequest> CreateAsync(
        string[] arguments,
        long? sourceWindowHandle,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        string? hostPipeName = PicaLaunchArguments.GetHostPipeName(arguments);

        if (hostPipeName is not null)
        {
            _logger.LogInformation("Creating a hosted Pica viewer session");
            PicaHostConnection connection = await PicaHostConnection
                .ConnectAsync(hostPipeName, ct)
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
            ? GetDirectoryImagePaths(
                requestedPaths[0],
                sourceWindowHandle)
            : requestedPaths;
        List<PicaImageItem> items = imagePaths
            .Select(CreateImageItem)
            .ToList();
        Guid selectedItemId = selectedPath is null
            ? Guid.Empty
            : CreateStableItemId(selectedPath);
        PicaViewerRequest viewerRequest = new(
            items,
            selectedItemId);
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

    private IReadOnlyList<string> GetDirectoryImagePaths(
        string selectedPath,
        long? sourceWindowHandle)
    {
        string? directoryPath = Path.GetDirectoryName(selectedPath);

        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return new List<string> { selectedPath };
        }

        try
        {
            List<string> fallbackImagePaths = Directory
                .EnumerateFiles(directoryPath, "*", SearchOption.TopDirectoryOnly)
                .Where(_formatRegistry.IsSupportedFileName)
                .Select(Path.GetFullPath)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .ThenBy(path => Path.GetFileName(path), StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            if (!fallbackImagePaths.Contains(
                    selectedPath,
                    StringComparer.OrdinalIgnoreCase))
            {
                return new List<string> { selectedPath };
            }

            return GetExplorerOrderedImagePaths(
                    directoryPath,
                    selectedPath,
                    fallbackImagePaths,
                    sourceWindowHandle)
                ?? fallbackImagePaths;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(
                ex,
                "Failed to enumerate adjacent images; using only the selected image");

            return new List<string> { selectedPath };
        }
    }

    private IReadOnlyList<string>? GetExplorerOrderedImagePaths(
        string directoryPath,
        string selectedPath,
        IReadOnlyList<string> fallbackImagePaths,
        long? sourceWindowHandle)
    {
        if (!OperatingSystem.IsWindows()
            || sourceWindowHandle is null)
        {
            return null;
        }

        IReadOnlyList<string>? explorerItemPaths =
            _explorerItemOrderProvider.GetItemPaths(
                directoryPath,
                sourceWindowHandle.Value);

        if (explorerItemPaths is null)
        {
            return null;
        }

        HashSet<string> remainingImagePaths = new(
            fallbackImagePaths,
            StringComparer.OrdinalIgnoreCase);
        List<string> orderedImagePaths = [];

        foreach (string explorerItemPath in explorerItemPaths)
        {
            string fullItemPath;

            try
            {
                fullItemPath = Path.GetFullPath(explorerItemPath);
            }
            catch (Exception ex) when (ex is ArgumentException
                or NotSupportedException
                or PathTooLongException)
            {
                _logger.LogDebug(
                    ex,
                    "File Explorer returned an item without a usable file-system path.");
                continue;
            }

            if (remainingImagePaths.Remove(fullItemPath))
            {
                orderedImagePaths.Add(fullItemPath);
            }
        }

        if (!orderedImagePaths.Contains(
                selectedPath,
                StringComparer.OrdinalIgnoreCase))
        {
            return null;
        }

        foreach (string fallbackImagePath in fallbackImagePaths)
        {
            if (remainingImagePaths.Remove(fallbackImagePath))
            {
                orderedImagePaths.Add(fallbackImagePath);
            }
        }

        return orderedImagePaths;
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
