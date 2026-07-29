namespace Pica.Viewer.Services;

internal sealed class ImagePreviewCache : IDisposable
{
    private readonly Dictionary<string, DecodedImagePreview> _previews =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly object _sync = new();
    private bool _disposed;

    public void Dispose()
    {
        List<DecodedImagePreview> previews;

        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            previews = _previews.Values.ToList();
            _previews.Clear();
        }

        DisposePreviews(previews);
    }

    internal bool Contains(string fullPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);

        lock (_sync)
        {
            return !_disposed
                && _previews.ContainsKey(fullPath);
        }
    }

    internal DecodedImagePreview? Take(string fullPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);

        lock (_sync)
        {
            if (_disposed
                || !_previews.Remove(
                    fullPath,
                    out DecodedImagePreview? preview))
            {
                return null;
            }

            return preview;
        }
    }

    internal void Store(string fullPath, DecodedImagePreview preview)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);
        ArgumentNullException.ThrowIfNull(preview);
        DecodedImagePreview? previousPreview = null;
        bool shouldDisposePreview;

        lock (_sync)
        {
            shouldDisposePreview = _disposed;

            if (!shouldDisposePreview)
            {
                _previews.Remove(
                    fullPath,
                    out previousPreview);
                _previews.Add(fullPath, preview);
            }
        }

        previousPreview?.Bitmap.Dispose();

        if (shouldDisposePreview)
        {
            preview.Bitmap.Dispose();
        }
    }

    internal void Retain(IReadOnlyCollection<string> retainedPaths)
    {
        ArgumentNullException.ThrowIfNull(retainedPaths);
        List<DecodedImagePreview> removedPreviews = [];

        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            List<string> cachedPaths = _previews.Keys.ToList();

            foreach (string cachedPath in cachedPaths)
            {
                if (retainedPaths.Contains(
                    cachedPath,
                    StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                DecodedImagePreview preview = _previews[cachedPath];
                _previews.Remove(cachedPath);
                removedPreviews.Add(preview);
            }
        }

        DisposePreviews(removedPreviews);
    }

    internal void Clear()
    {
        List<DecodedImagePreview> previews;

        lock (_sync)
        {
            previews = _previews.Values.ToList();
            _previews.Clear();
        }

        DisposePreviews(previews);
    }

    private static void DisposePreviews(
        IReadOnlyList<DecodedImagePreview> previews)
    {
        foreach (DecodedImagePreview preview in previews)
        {
            preview.Bitmap.Dispose();
        }
    }
}
