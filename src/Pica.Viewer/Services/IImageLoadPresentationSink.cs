using Pica.Protocol;

namespace Pica.Viewer.Services;

internal interface IImageLoadPresentationSink
{
    void BeginImageLoad(PicaImageItem item);

    void ApplyPreview(
        PicaImageItem item,
        string fullPath,
        DecodedImagePreview preview);

    void ApplyFullResolution(
        PicaImageItem item,
        string fullPath,
        DecodedImagePreview? displayedPreview,
        DecodedImageContent content);
}
