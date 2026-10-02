using Avalonia.Input.Platform;

namespace Pica.Viewer.Services;

public interface IClipboardImageReader
{
    Task<IReadOnlyList<ClipboardImageInput>> ReadAsync(
        IClipboard clipboard,
        CancellationToken ct);
}
