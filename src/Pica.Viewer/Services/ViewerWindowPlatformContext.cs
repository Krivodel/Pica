using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;

namespace Pica.Viewer.Services;

internal sealed class ViewerWindowPlatformContext
{
    private readonly IStorageProvider? _storageProvider;
    private readonly IClipboard? _clipboard;
    private readonly TaskCompletionSource<TopLevel> _topLevelSource = new(
        TaskCreationOptions.RunContinuationsAsynchronously);

    internal ViewerWindowPlatformContext()
    {
    }

    internal ViewerWindowPlatformContext(
        IStorageProvider? storageProvider,
        IClipboard? clipboard)
    {
        _storageProvider = storageProvider;
        _clipboard = clipboard;
    }

    internal void Initialize(TopLevel topLevel)
    {
        ArgumentNullException.ThrowIfNull(topLevel);

        if (!_topLevelSource.TrySetResult(topLevel))
        {
            throw new InvalidOperationException(
                "The viewer window platform context has already been initialized.");
        }
    }

    internal async Task<IStorageProvider?> GetStorageProviderAsync(
        CancellationToken ct)
    {
        if (_storageProvider is not null)
        {
            return _storageProvider;
        }

        TopLevel topLevel = await _topLevelSource.Task
            .WaitAsync(ct)
            .ConfigureAwait(false);

        return topLevel.StorageProvider;
    }

    internal async Task<IClipboard?> GetClipboardAsync(
        CancellationToken ct)
    {
        if (_clipboard is not null)
        {
            return _clipboard;
        }

        TopLevel topLevel = await _topLevelSource.Task
            .WaitAsync(ct)
            .ConfigureAwait(false);

        return topLevel.Clipboard;
    }
}
