using Pica.Protocol;

namespace Pica.Viewer.Services;

internal interface IImagePreviewLoader
{
    Task<DecodedImagePreview> LoadAsync(
        PicaImageItem item,
        CancellationToken ct);
}
