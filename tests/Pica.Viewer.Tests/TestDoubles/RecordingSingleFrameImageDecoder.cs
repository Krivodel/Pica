using Avalonia;
using Avalonia.Media.Imaging;

using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.TestDoubles;

internal sealed class RecordingSingleFrameImageDecoder : IImageDecoder
{
    internal int DecodeCount { get; private set; }

    private readonly Bitmap _bitmap;

    internal RecordingSingleFrameImageDecoder(Bitmap bitmap)
    {
        _bitmap = bitmap ?? throw new ArgumentNullException(nameof(bitmap));
    }

    public PixelSize ReadPixelSize(
        Stream sourceStream,
        CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public bool ReadHasAlpha(
        Stream sourceStream,
        CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public Bitmap Decode(
        Stream sourceStream,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sourceStream);
        ct.ThrowIfCancellationRequested();
        DecodeCount++;

        return _bitmap;
    }

    public Bitmap DecodeToWidth(
        Stream sourceStream,
        int width,
        CancellationToken ct)
    {
        throw new NotSupportedException();
    }
}
