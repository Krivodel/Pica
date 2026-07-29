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
    internal Bitmap? FullResolutionBitmap { get; private set; }

    public void Dispose()
    {
        Preview?.Bitmap.Dispose();
        FullResolutionBitmap?.Dispose();
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
        Bitmap bitmap)
    {
        LastItem = item ?? throw new ArgumentNullException(nameof(item));
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);
        Preview = displayedPreview;
        FullResolutionBitmap = bitmap
            ?? throw new ArgumentNullException(nameof(bitmap));
        FullResolutionCount++;
    }
}
