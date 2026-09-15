using Pica.Protocol;
using Pica.Viewer.Views;

namespace Pica.Viewer.Services;

public interface IImageViewerWindowFactory
{
    Task<ImageViewerWindow> CreateAsync(
        PicaViewerRequest request,
        IViewerActionDispatcher actionDispatcher,
        CancellationToken ct);

    Task<ImageViewerWindow> CreateAsync(
        PicaViewerRequest request,
        IViewerActionDispatcher actionDispatcher,
        IReadOnlyDictionary<Guid, IPicaImageBitmapSource>? bitmapSources,
        CancellationToken ct);
}
