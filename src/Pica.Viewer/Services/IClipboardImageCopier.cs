using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;

namespace Pica.Viewer.Services;

public interface IClipboardImageCopier
{
    Task CopyFileAsync(
        string imagePath,
        IClipboard clipboard,
        IStorageProvider storageProvider,
        CancellationToken ct);
}
