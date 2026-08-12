using Avalonia;
using Avalonia.Media.Imaging;

using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.TestDoubles;

internal sealed class BlockingImageDecoder :
    IImageDecoder,
    IDisposable
{
    internal Task OperationStarted => _operationStarted.Task;

    private readonly Bitmap _bitmap;
    private readonly TaskCompletionSource _operationStarted = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ManualResetEventSlim _release = new();

    internal BlockingImageDecoder(Bitmap bitmap)
    {
        _bitmap = bitmap ?? throw new ArgumentNullException(nameof(bitmap));
    }

    public PixelSize ReadPixelSize(
        Stream sourceStream,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sourceStream);
        ct.ThrowIfCancellationRequested();

        return _bitmap.PixelSize;
    }

    public bool ReadHasAlpha(
        Stream sourceStream,
        CancellationToken ct)
    {
        WaitForRelease(sourceStream, ct);

        return true;
    }

    public Bitmap Decode(
        Stream sourceStream,
        CancellationToken ct)
    {
        WaitForRelease(sourceStream, ct);

        return _bitmap;
    }

    public Bitmap DecodeToWidth(
        Stream sourceStream,
        int width,
        CancellationToken ct)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(width),
                width,
                "The decode width must be positive.");
        }

        WaitForRelease(sourceStream, ct);

        return _bitmap;
    }

    public void Dispose()
    {
        _release.Dispose();
    }

    internal void Release()
    {
        _release.Set();
    }

    private void WaitForRelease(
        Stream sourceStream,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sourceStream);
        ct.ThrowIfCancellationRequested();
        _operationStarted.TrySetResult();
        _release.Wait(ct);
    }
}
