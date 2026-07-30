using Avalonia.Media.Imaging;

namespace Pica.Viewer.Tests.TestDoubles;

internal sealed class TrackingBitmap : Bitmap
{
    internal bool IsDisposed { get; private set; }

    private readonly TaskCompletionSource<bool> _disposal = new(
        TaskCreationOptions.RunContinuationsAsynchronously);

    internal TrackingBitmap(string fileName)
        : base(fileName)
    {
    }

    public override void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        IsDisposed = true;
        base.Dispose();
        _disposal.TrySetResult(true);
    }

    internal async Task WaitForDisposalAsync(CancellationToken ct)
    {
        await _disposal.Task.WaitAsync(ct).ConfigureAwait(false);
    }
}
