using Avalonia;
using Avalonia.Media.Imaging;

using Pica.Viewer.Services;
using Pica.Viewer.ViewModels;

namespace Pica.Viewer.Tests.TestDoubles;

internal sealed class RecordingImageChannelBitmapLoader : IImageChannelBitmapLoader
{
    internal int AlphaReadCount { get; private set; }
    internal int ChannelLoadCount { get; private set; }
    internal ImageChannel? LastChannel { get; private set; }
    internal bool HasAlpha { get; init; }
    internal bool CompleteAsynchronously { get; init; }

    public async Task<bool> ReadHasAlphaAsync(
        string fullPath,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);
        ct.ThrowIfCancellationRequested();

        if (CompleteAsynchronously)
        {
            await Task.Run(
                () => ct.ThrowIfCancellationRequested(),
                ct).ConfigureAwait(false);
        }

        AlphaReadCount++;

        return HasAlpha;
    }

    public async Task<Bitmap> LoadAsync(
        Bitmap sourceBitmap,
        ImageChannel channel,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sourceBitmap);
        ArgumentNullException.ThrowIfNull(channel);
        ct.ThrowIfCancellationRequested();

        if (CompleteAsynchronously)
        {
            await Task.Run(
                () => ct.ThrowIfCancellationRequested(),
                ct).ConfigureAwait(false);
        }

        ChannelLoadCount++;
        LastChannel = channel;
        Bitmap bitmap = BitmapPixelCopy.CreateCrop(
            sourceBitmap,
            new PixelRect(
                0,
                0,
                sourceBitmap.PixelSize.Width,
                sourceBitmap.PixelSize.Height));

        return bitmap;
    }
}
