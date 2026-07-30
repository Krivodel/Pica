using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;

namespace Pica.Viewer.Services;

internal sealed class ViewerWindowPlatformContext
{
    private readonly TopLevel? _topLevel;
    private readonly IStorageProvider? _storageProvider;
    private readonly IClipboard? _clipboard;

    internal ViewerWindowPlatformContext(TopLevel topLevel)
    {
        _topLevel = topLevel
            ?? throw new ArgumentNullException(nameof(topLevel));
    }

    internal ViewerWindowPlatformContext(
        IStorageProvider? storageProvider,
        IClipboard? clipboard)
    {
        _storageProvider = storageProvider;
        _clipboard = clipboard;
    }

    internal Task<IStorageProvider?> GetStorageProviderAsync(
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        IStorageProvider? storageProvider =
            _storageProvider ?? _topLevel?.StorageProvider;

        return Task.FromResult(storageProvider);
    }

    internal Task<IClipboard?> GetClipboardAsync(
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        IClipboard? clipboard = _clipboard ?? _topLevel?.Clipboard;

        return Task.FromResult(clipboard);
    }
}
