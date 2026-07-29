using Avalonia.Media.Imaging;

namespace Pica.Viewer.Services;

internal sealed class ClipboardImagePreparer
{
    public async Task<PreparedBitmapPixels> PrepareBitmapAsync(
        Bitmap bitmap,
        CancellationToken ct)
    {
        return await RunPreparationAsync(
            bitmap,
            static (preparedBitmap, operationCt) =>
            {
                operationCt.ThrowIfCancellationRequested();
                return preparedBitmap;
            },
            ct).ConfigureAwait(false);
    }

    public async Task<PreparedClipboardImage> PrepareImageAsync(
        Bitmap bitmap,
        CancellationToken ct)
    {
        return await RunPreparationAsync(
            bitmap,
            static (preparedBitmap, operationCt) => new PreparedClipboardImage(
                preparedBitmap.PixelSize,
                preparedBitmap.RowBytes,
                preparedBitmap.BgraPixels,
                PngImageEncoder.EncodePixels(preparedBitmap, operationCt)),
            ct).ConfigureAwait(false);
    }

    private static async Task<TResult> RunPreparationAsync<TResult>(
        Bitmap bitmap,
        Func<PreparedBitmapPixels, CancellationToken, TResult> createResult,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        ArgumentNullException.ThrowIfNull(createResult);

        return await Task.Run(
            () => createResult(BitmapPixelReader.Read(bitmap, ct), ct),
            ct).ConfigureAwait(false);
    }
}
