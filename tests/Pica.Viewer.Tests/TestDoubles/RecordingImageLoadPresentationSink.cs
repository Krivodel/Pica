using Avalonia.Media.Imaging;

using Pica.Protocol;
using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.TestDoubles;

internal sealed class RecordingImageLoadPresentationSink :
    IImageLoadPresentationSink,
    IDisposable
{
    internal int BeginCount { get; private set; }
    internal int PreviewCount { get; private set; }
    internal int FullResolutionCount { get; private set; }
    internal PicaImageItem? LastItem { get; private set; }
    internal DecodedImagePreview? Preview { get; private set; }
    internal Bitmap? FullResolutionBitmap =>
        FullResolutionImage?.Frames[0].Bitmap;
    internal DecodedImage? FullResolutionImage { get; private set; }
    internal DecodedImageContent? FullResolutionContent { get; private set; }

    public void Dispose()
    {
        Preview?.Bitmap.Dispose();
        if (FullResolutionContent is not null)
        {
            foreach (DecodedImageContentGroup group
                in FullResolutionContent.Groups)
            {
                foreach (DecodedImage image
                    in group.StopAndGetLoadedImages())
                {
                    image.Dispose();
                }
            }
        }
    }

    public void BeginImageLoad(PicaImageItem item)
    {
        LastItem = item ?? throw new ArgumentNullException(nameof(item));
        BeginCount++;
    }

    public void ApplyPreview(
        PicaImageItem item,
        string fullPath,
        DecodedImagePreview preview)
    {
        LastItem = item ?? throw new ArgumentNullException(nameof(item));
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);
        Preview = preview
            ?? throw new ArgumentNullException(nameof(preview));
        PreviewCount++;
    }

    public void ApplyFullResolution(
        PicaImageItem item,
        string fullPath,
        DecodedImagePreview? displayedPreview,
        DecodedImageContent content)
    {
        LastItem = item ?? throw new ArgumentNullException(nameof(item));
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);
        Preview = displayedPreview;
        FullResolutionContent = content
            ?? throw new ArgumentNullException(nameof(content));
        FullResolutionImage = content.Groups[
            content.InitialGroupIndex].GetRequiredImage();
        FullResolutionCount++;
    }
}
