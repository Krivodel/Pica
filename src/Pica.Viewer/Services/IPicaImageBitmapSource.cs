using Avalonia.Media.Imaging;

namespace Pica.Viewer.Services;

public interface IPicaImageBitmapSource
{
    bool IsFileBacked { get; }

    ValueTask<IPicaImageBitmapLease> AcquireAsync(
        CancellationToken ct);
}

public interface IPicaImageBitmapLease : IDisposable
{
    Bitmap Bitmap { get; }
}
