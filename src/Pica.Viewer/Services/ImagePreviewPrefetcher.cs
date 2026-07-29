using Microsoft.Extensions.Logging;

using Pica.Protocol;

namespace Pica.Viewer.Services;

internal sealed class ImagePreviewPrefetcher : IDisposable
{
    private readonly ImageViewerSession _session;
    private readonly ImagePreviewLoader _previewLoader;
    private readonly ILogger<ImagePreviewPrefetcher> _logger;
    private readonly ImagePreviewCache _previewCache = new();

    internal ImagePreviewPrefetcher(
        ImageViewerSession session,
        ImagePreviewLoader previewLoader,
        ILogger<ImagePreviewPrefetcher> logger)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _previewLoader = previewLoader
            ?? throw new ArgumentNullException(nameof(previewLoader));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void Dispose()
    {
        _previewCache.Dispose();
    }

    internal DecodedImagePreview? Take(string fullPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);

        return _previewCache.Take(fullPath);
    }

    internal void Clear()
    {
        _previewCache.Clear();
    }

    internal async Task PrefetchAdjacentAsync(
        int selectedIndex,
        Func<bool> canContinue,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(canContinue);
        List<PicaImageItem> adjacentItems =
            await PrepareAdjacentPreviewCacheAsync(
                selectedIndex,
                ct).ConfigureAwait(false);

        foreach (PicaImageItem adjacentItem in adjacentItems)
        {
            await PrefetchPreviewAsync(
                adjacentItem,
                canContinue,
                ct).ConfigureAwait(false);
        }
    }

    private static Task<List<PicaImageItem>> FilterExistingItemsAsync(
        IReadOnlyList<PicaImageItem> items,
        CancellationToken ct)
    {
        return Task.Run(
            () =>
            {
                List<PicaImageItem> existingItems = [];

                foreach (PicaImageItem item in items)
                {
                    ct.ThrowIfCancellationRequested();

                    if (File.Exists(Path.GetFullPath(item.FilePath)))
                    {
                        existingItems.Add(item);
                    }
                }

                return existingItems;
            },
            ct);
    }

    private async Task<List<PicaImageItem>> PrepareAdjacentPreviewCacheAsync(
        int selectedIndex,
        CancellationToken ct)
    {
        List<PicaImageItem> candidateItems =
            GetAdjacentImageItems(selectedIndex);
        List<PicaImageItem> adjacentItems =
            await FilterExistingItemsAsync(
                candidateItems,
                ct).ConfigureAwait(false);
        List<string> adjacentPaths = adjacentItems
            .Select(item => Path.GetFullPath(item.FilePath))
            .ToList();
        _previewCache.Retain(adjacentPaths);

        return adjacentItems;
    }

    private async Task PrefetchPreviewAsync(
        PicaImageItem item,
        Func<bool> canContinue,
        CancellationToken ct)
    {
        string fullPath = Path.GetFullPath(item.FilePath);

        if (_previewCache.Contains(fullPath))
        {
            return;
        }

        DecodedImagePreview? preview = await TryDecodePreviewAsync(
            item,
            ct).ConfigureAwait(false);

        if (preview is null)
        {
            return;
        }

        if (!canContinue())
        {
            preview.Bitmap.Dispose();
            _previewCache.Clear();
            return;
        }

        _previewCache.Store(fullPath, preview);
    }

    private List<PicaImageItem> GetAdjacentImageItems(int selectedIndex)
    {
        IReadOnlyList<PicaImageItem> items = _session.Items;
        List<PicaImageItem> adjacentItems = [];

        if (items.Count <= 1)
        {
            return adjacentItems;
        }

        int normalizedIndex = Math.Clamp(
            selectedIndex,
            0,
            items.Count - 1);
        int[] offsets =
        [
            _session.PreferredNavigationDirection,
            -_session.PreferredNavigationDirection
        ];

        foreach (int offset in offsets)
        {
            int adjacentIndex =
                (normalizedIndex + offset + items.Count)
                % items.Count;
            PicaImageItem adjacentItem = items[adjacentIndex];
            string adjacentPath = Path.GetFullPath(
                adjacentItem.FilePath);

            if (!adjacentItems.Any(item => string.Equals(
                    Path.GetFullPath(item.FilePath),
                    adjacentPath,
                    StringComparison.OrdinalIgnoreCase)))
            {
                adjacentItems.Add(adjacentItem);
            }
        }

        return adjacentItems;
    }

    private async Task<DecodedImagePreview?> TryDecodePreviewAsync(
        PicaImageItem item,
        CancellationToken ct)
    {
        try
        {
            return await _previewLoader
                .LoadAsync(item, ct)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to preload the preview for image {ItemId}.",
                item.Id);

            return null;
        }
    }
}
