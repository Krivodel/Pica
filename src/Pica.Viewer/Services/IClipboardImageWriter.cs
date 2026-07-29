namespace Pica.Viewer.Services;

public interface IClipboardImageWriter
{
    Task FlushAsync(CancellationToken ct);
}
