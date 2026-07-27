using System.Runtime.InteropServices;

using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Pica.Viewer.Services;

internal sealed class ClipboardImagePreparer
{
    private const int BytesPerPixel = 4;

    public async Task<PreparedClipboardBitmap> PrepareBitmapAsync(
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
        Func<PreparedClipboardBitmap, CancellationToken, TResult> createResult,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        ArgumentNullException.ThrowIfNull(createResult);

        return await Task.Run(
            () => createResult(PrepareBitmap(bitmap, ct), ct),
            ct).ConfigureAwait(false);
    }

    private static PreparedClipboardBitmap PrepareBitmap(
        Bitmap bitmap,
        CancellationToken ct)
    {
        WriteableBitmap? readableCopy = null;

        try
        {
            WriteableBitmap readableBitmap;

            if (bitmap is WriteableBitmap writeableBitmap)
            {
                readableBitmap = writeableBitmap;
            }
            else
            {
                readableCopy = BitmapPixelCopy.CreateCopy(bitmap);
                readableBitmap = readableCopy;
            }

            return CopyPixels(readableBitmap, ct);
        }
        finally
        {
            readableCopy?.Dispose();
        }
    }

    private static PreparedClipboardBitmap CopyPixels(
        WriteableBitmap bitmap,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using ILockedFramebuffer framebuffer = bitmap.Lock();

        if (framebuffer.Format != PixelFormat.Bgra8888)
        {
            throw new NotSupportedException(
                $"Unsupported clipboard pixel format: {framebuffer.Format}.");
        }

        int rowBytes = checked(framebuffer.Size.Width * BytesPerPixel);
        int contentLength = checked(rowBytes * framebuffer.Size.Height);
        byte[] pixels = new byte[contentLength];
        CopyPixels(framebuffer, pixels, rowBytes, ct);

        return new PreparedClipboardBitmap(framebuffer.Size, rowBytes, pixels);
    }

    private static void CopyPixels(
        ILockedFramebuffer framebuffer,
        byte[] destination,
        int destinationRowBytes,
        CancellationToken ct)
    {
        if (framebuffer.RowBytes == destinationRowBytes)
        {
            Marshal.Copy(framebuffer.Address, destination, 0, destination.Length);
            ct.ThrowIfCancellationRequested();
            return;
        }

        for (int row = 0; row < framebuffer.Size.Height; row++)
        {
            ct.ThrowIfCancellationRequested();
            IntPtr sourceAddress = IntPtr.Add(framebuffer.Address, row * framebuffer.RowBytes);
            Marshal.Copy(
                sourceAddress,
                destination,
                row * destinationRowBytes,
                destinationRowBytes);
        }
    }
}
