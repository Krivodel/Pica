using Avalonia;
using Avalonia.Media.Imaging;

using Pica.Viewer.Services;
using Pica.Viewer.ViewModels;

namespace Pica.Viewer.Views;

internal sealed class ImageSelectionClipboardPreparation : IDisposable
{
    private readonly ImageViewerActionsViewModel _actions;
    private readonly ImagePresentationController _imagePresentation;
    private readonly ImageViewportController _viewport;
    private OperationCancellation? _cancellation;
    private Task<PreparedClipboardImage?>? _preparationTask;
    private PixelRect _preparedRect;

    internal ImageSelectionClipboardPreparation(
        ImageViewerActionsViewModel actions,
        ImagePresentationController imagePresentation,
        ImageViewportController viewport)
    {
        _actions = actions
            ?? throw new ArgumentNullException(nameof(actions));
        _imagePresentation = imagePresentation
            ?? throw new ArgumentNullException(nameof(imagePresentation));
        _viewport = viewport
            ?? throw new ArgumentNullException(nameof(viewport));
    }

    public void Dispose()
    {
        Cancel();
    }

    internal void Schedule(PixelRect? sourceRect)
    {
        Bitmap? sourceBitmap = _viewport.CurrentBitmap;

        if (!_imagePresentation.IsFullResolutionReady
            || (sourceBitmap is null)
            || (sourceRect is not { } validSourceRect))
        {
            Cancel();
            return;
        }

        if ((_preparationTask is not null)
            && (_preparedRect == validSourceRect))
        {
            return;
        }

        Cancel();
        OperationCancellation cancellation = new();
        ImagePixelSelection selection = new(
            validSourceRect.X,
            validSourceRect.Y,
            validSourceRect.Width,
            validSourceRect.Height);
        _cancellation = cancellation;
        _preparedRect = validSourceRect;
        _preparationTask = CompleteAsync(
            selection,
            cancellation);
    }

    internal void Cancel()
    {
        OperationCancellation? cancellation = _cancellation;
        _cancellation = null;
        _preparationTask = null;
        _preparedRect = new PixelRect();
        cancellation?.Cancel();
    }

    internal async Task<PreparedClipboardImage?> GetAsync(
        PixelRect? sourceRect,
        CancellationToken ct)
    {
        if (sourceRect is not { } validSourceRect)
        {
            return null;
        }

        if ((_preparationTask is null)
            || (_preparedRect != validSourceRect))
        {
            Schedule(validSourceRect);
        }

        Task<PreparedClipboardImage?>? preparationTask =
            _preparationTask;

        return preparationTask is null
            ? null
            : await preparationTask.WaitAsync(ct);
    }

    private async Task<PreparedClipboardImage?> CompleteAsync(
        ImagePixelSelection selection,
        OperationCancellation cancellation)
    {
        try
        {
            return await _actions.PrepareSelectionImageAsync(
                selection,
                cancellation.Token);
        }
        finally
        {
            if (object.ReferenceEquals(
                _cancellation,
                cancellation))
            {
                _cancellation = null;
            }

            cancellation.Complete();
        }
    }
}
