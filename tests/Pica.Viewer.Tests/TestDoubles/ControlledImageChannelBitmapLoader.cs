using Avalonia.Media.Imaging;

using Pica.Viewer.Services;
using Pica.Viewer.ViewModels;

namespace Pica.Viewer.Tests.TestDoubles;

internal sealed class ControlledImageChannelBitmapLoader :
    IImageChannelBitmapLoader
{
    internal bool IsCancellationRequested =>
        _cancellationToken.IsCancellationRequested;

    private readonly TaskCompletionSource<Bitmap> _completion = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _started = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private CancellationToken _cancellationToken;

    public Task<bool> ReadHasAlphaAsync(
        string fullPath,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);
        ct.ThrowIfCancellationRequested();

        return Task.FromResult(true);
    }

    public Task<Bitmap> LoadAsync(
        Bitmap sourceBitmap,
        ImageChannel channel,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sourceBitmap);
        ArgumentNullException.ThrowIfNull(channel);
        _cancellationToken = ct;
        _started.TrySetResult();

        return _completion.Task;
    }

    internal void Complete(Bitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        _completion.TrySetResult(bitmap);
    }

    internal async Task WaitUntilStartedAsync(CancellationToken ct)
    {
        await _started.Task
            .WaitAsync(ct)
            .ConfigureAwait(false);
    }

}
