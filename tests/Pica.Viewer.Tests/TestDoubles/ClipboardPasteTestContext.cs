using Microsoft.Extensions.Logging.Abstractions;

using Pica.Protocol;
using Pica.Viewer.Services;
using Pica.Viewer.Tests.Services;

namespace Pica.Viewer.Tests.TestDoubles;

internal sealed class ClipboardPasteTestContext : IDisposable
{
    internal ImageViewerSession Session { get; }
    internal DelegateClipboardImageReader Reader { get; } = new();
    internal ImagePresentationController Presentation { get; }
    internal ImageLoadCoordinator LoadCoordinator { get; }
    internal ViewerClipboardPasteService PasteService { get; }

    internal ClipboardPasteTestContext(IReadOnlyList<PicaImageItem> items)
    {
        Session = new ImageViewerSession(new PicaViewerRequest(items, items.FirstOrDefault()?.Id ?? Guid.Empty), true);
        ImageFormatRegistry formats = new();
        AvaloniaViewerUiDispatcher dispatcher = new();
        FullResolutionImageLoader loader = new(formats, MultiFrameImageDecoderTestFactory.Create());
        Presentation = new ImagePresentationController(
            Session, new ImageChannelBitmapLoader(formats), dispatcher,
            NullLogger<ImagePresentationController>.Instance);
        LoadCoordinator = new ImageLoadCoordinator(
            Session, new ImagePreviewLoader(formats, NullLogger<ImagePreviewLoader>.Instance), loader,
            Presentation, new RecordingViewerRenderFrameAwaiter(), dispatcher,
            NullLogger<ImageLoadCoordinator>.Instance, NullLogger<ImagePreviewPrefetcher>.Instance, false);
        PasteService = new ViewerClipboardPasteService(
            Session, LoadCoordinator, new ViewerWindowPlatformContext(null, new RecordingClipboard().Clipboard),
            Reader, loader, new ClipboardImageFormatCatalog(formats), dispatcher,
            NullLogger<ViewerClipboardPasteService>.Instance);
        LoadCoordinator.Start();
    }

    public void Dispose()
    {
        PasteService.Dispose();
        LoadCoordinator.Dispose();
        Presentation.Dispose();
    }
}
