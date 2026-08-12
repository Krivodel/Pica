using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.TestDoubles;

internal sealed class BlockingMultiFrameImageDecoder :
    IMultiFrameImageDecoder,
    IDisposable
{
    internal Task OperationStarted => _operationStarted.Task;

    private readonly DecodedImage _image;
    private readonly TaskCompletionSource _operationStarted = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ManualResetEventSlim _release = new();

    internal BlockingMultiFrameImageDecoder(DecodedImage image)
    {
        _image = image ?? throw new ArgumentNullException(nameof(image));
    }

    public DecodedImage Decode(
        Stream sourceStream,
        ImageDecoderSelection decoderSelection,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sourceStream);
        ArgumentNullException.ThrowIfNull(decoderSelection);
        ct.ThrowIfCancellationRequested();
        _operationStarted.TrySetResult();
        _release.Wait(ct);

        return _image;
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
