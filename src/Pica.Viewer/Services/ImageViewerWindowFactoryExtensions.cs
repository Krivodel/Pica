using Pica.Protocol;
using Pica.Viewer.Views;

namespace Pica.Viewer.Services;

public static class ImageViewerWindowFactoryExtensions
{
    public static Task<ImageViewerWindow> CreateAsync(
        this IImageViewerWindowFactory factory,
        PicaViewerRequest request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(request);

        if (request.Actions.Count > 0)
        {
            throw new ArgumentException(
                "A viewer action dispatcher is required when the request contains actions.",
                nameof(request));
        }

        return factory.CreateAsync(
            request,
            NullViewerActionDispatcher.Instance,
            ct);
    }
}
