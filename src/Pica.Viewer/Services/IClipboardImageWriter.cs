using Avalonia.Input.Platform;

namespace Pica.Viewer.Services;

public interface IClipboardImageWriter
{
    void Attach(IClipboard clipboard);
    Task FlushAsync(CancellationToken ct);
}
