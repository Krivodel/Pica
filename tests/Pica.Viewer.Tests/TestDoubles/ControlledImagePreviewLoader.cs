using Pica.Protocol;
using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.TestDoubles;

internal sealed class ControlledImagePreviewLoader : IImagePreviewLoader
{
    private readonly Dictionary<Guid, TaskCompletionSource<DecodedImagePreview>>
        _completions;
    private readonly Dictionary<Guid, TaskCompletionSource<bool>> _starts;

    internal ControlledImagePreviewLoader(
        IReadOnlyList<PicaImageItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        _completions =
            new Dictionary<Guid, TaskCompletionSource<DecodedImagePreview>>();
        _starts = new Dictionary<Guid, TaskCompletionSource<bool>>();

        foreach (PicaImageItem item in items)
        {
            _completions.Add(
                item.Id,
                new TaskCompletionSource<DecodedImagePreview>(
                    TaskCreationOptions.RunContinuationsAsynchronously));
            _starts.Add(
                item.Id,
                new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously));
        }
    }

    public async Task<DecodedImagePreview> LoadAsync(
        PicaImageItem item,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(item);
        _starts[item.Id].TrySetResult(true);

        return await _completions[item.Id].Task
            .WaitAsync(ct)
            .ConfigureAwait(false);
    }

    internal void Complete(
        PicaImageItem item,
        DecodedImagePreview preview)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(preview);
        _completions[item.Id].TrySetResult(preview);
    }

    internal async Task WaitUntilStartedAsync(
        PicaImageItem item,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(item);
        await _starts[item.Id].Task
            .WaitAsync(ct)
            .ConfigureAwait(false);
    }
}
