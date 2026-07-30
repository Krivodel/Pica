using Pica.Viewer.ViewModels;

namespace Pica.Viewer.Services;

internal sealed class ImageViewerInteractionServices
{
    internal ImageViewerActionsViewModel Actions { get; }
    internal ImageViewerOpenWithViewModel OpenWith { get; }

    private readonly ViewerImageCommandService _commandService;
    private readonly ViewerClipboardServices _clipboardServices;
    private bool _areViewModelsDetached;
    private bool _isDisposed;

    internal ImageViewerInteractionServices(
        ImageViewerActionsViewModel actions,
        ImageViewerOpenWithViewModel openWith,
        ViewerImageCommandService commandService,
        ViewerClipboardServices clipboardServices)
    {
        Actions = actions ?? throw new ArgumentNullException(nameof(actions));
        OpenWith = openWith ?? throw new ArgumentNullException(nameof(openWith));
        _commandService = commandService
            ?? throw new ArgumentNullException(nameof(commandService));
        _clipboardServices = clipboardServices
            ?? throw new ArgumentNullException(nameof(clipboardServices));
    }

    internal void DisposeViewModels()
    {
        if (_areViewModelsDetached)
        {
            return;
        }

        Actions.Dispose();
        OpenWith.Dispose();
        _areViewModelsDetached = true;
    }

    internal async Task FlushAndDisposeAsync(CancellationToken ct)
    {
        if (_isDisposed)
        {
            return;
        }

        DisposeViewModels();

        try
        {
            await _clipboardServices
                .FlushAsync(ct)
                .ConfigureAwait(false);
        }
        finally
        {
            _commandService.Dispose();
            _clipboardServices.Dispose();
            _isDisposed = true;
        }
    }

    internal void DisposeWithoutFlush()
    {
        if (_isDisposed)
        {
            return;
        }

        DisposeViewModels();
        _commandService.Dispose();
        _clipboardServices.Dispose();
        _isDisposed = true;
    }
}
