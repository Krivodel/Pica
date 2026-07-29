using Avalonia.Media.Imaging;

namespace Pica.Viewer.Services;

internal sealed class ImagePresentationBitmapLease : IDisposable
{
    internal Bitmap Bitmap { get; }

    private Action<Bitmap>? _release;

    internal ImagePresentationBitmapLease(
        Bitmap bitmap,
        Action<Bitmap> release)
    {
        Bitmap = bitmap ?? throw new ArgumentNullException(nameof(bitmap));
        _release = release ?? throw new ArgumentNullException(nameof(release));
    }

    public void Dispose()
    {
        Action<Bitmap>? release = Interlocked.Exchange(
            ref _release,
            null);
        release?.Invoke(Bitmap);
    }
}
