namespace Pica.Viewer.Services;

internal sealed class ImagePreviewCache : IDisposable
{
    private readonly Dictionary<string, DecodedImagePreview> _previews =
        new(StringComparer.OrdinalIgnoreCase);

    public void Dispose()
    {
        Clear();
    }

    internal bool Contains(string fullPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);

        return _previews.ContainsKey(fullPath);
    }

    internal DecodedImagePreview? Take(string fullPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);

        if (!_previews.Remove(fullPath, out DecodedImagePreview? preview))
        {
            return null;
        }

        return preview;
    }

    internal void Store(string fullPath, DecodedImagePreview preview)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);
        ArgumentNullException.ThrowIfNull(preview);

        if (_previews.Remove(fullPath, out DecodedImagePreview? previousPreview))
        {
            previousPreview.Bitmap.Dispose();
        }

        _previews.Add(fullPath, preview);
    }

    internal void Retain(IReadOnlyCollection<string> retainedPaths)
    {
        ArgumentNullException.ThrowIfNull(retainedPaths);

        List<string> cachedPaths = _previews.Keys.ToList();

        foreach (string cachedPath in cachedPaths)
        {
            if (retainedPaths.Contains(cachedPath, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            DecodedImagePreview preview = _previews[cachedPath];
            _previews.Remove(cachedPath);
            preview.Bitmap.Dispose();
        }
    }

    internal void Clear()
    {
        foreach (DecodedImagePreview preview in _previews.Values)
        {
            preview.Bitmap.Dispose();
        }

        _previews.Clear();
    }
}
