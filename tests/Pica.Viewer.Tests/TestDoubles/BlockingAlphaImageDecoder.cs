using Avalonia;
using Avalonia.Media.Imaging;

using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.TestDoubles;

internal sealed class BlockingAlphaImageDecoder :
    IImageDecoder,
    IDisposable
{
    internal Task OperationStarted => _operationStarted.Task;

    private readonly TaskCompletionSource _operationStarted = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ManualResetEventSlim _release = new();

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
        ArgumentNullException.ThrowIfNull(sourceStream);
        ct.ThrowIfCancellationRequested();
        _operationStarted.TrySetResult();
        _release.Wait(ct);

        return true;
    }

    public Bitmap Decode(
        Stream sourceStream,
        CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public Bitmap DecodeToWidth(
        Stream sourceStream,
        int width,
        CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public void Dispose()
    {
        _release.Dispose();
    }

    internal void Release()
    {
        _release.Set();
    }
}
