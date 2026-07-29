using System.Runtime.InteropServices;

using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Pica.Viewer.Services;

internal static class BitmapPixelReader
{
    private const int BytesPerPixel = 4;

    internal static PreparedBitmapPixels Read(
        Bitmap bitmap,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
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

    internal static PreparedBitmapPixels ReadUnpremultiplied(
        Bitmap bitmap,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        using WriteableBitmap readableBitmap =
            BitmapPixelCopy.CreateUnpremultipliedCopy(bitmap);

        return CopyPixels(readableBitmap, ct);
    }

    internal static void ConvertToBgra(
        PixelFormat sourceFormat,
        byte[] pixels)
    {
        ArgumentNullException.ThrowIfNull(pixels);

        if (sourceFormat == PixelFormat.Bgra8888)
        {
            return;
        }

        if (sourceFormat != PixelFormat.Rgba8888)
        {
            throw new NotSupportedException(
                $"Unsupported bitmap pixel format: {sourceFormat}.");
        }

        for (int pixelOffset = 0; pixelOffset < pixels.Length; pixelOffset += BytesPerPixel)
        {
            byte red = pixels[pixelOffset];
            pixels[pixelOffset] = pixels[pixelOffset + 2];
            pixels[pixelOffset + 2] = red;
        }
    }

    private static PreparedBitmapPixels CopyPixels(
        WriteableBitmap bitmap,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using ILockedFramebuffer framebuffer = bitmap.Lock();

        int rowBytes = checked(framebuffer.Size.Width * BytesPerPixel);
        int contentLength = checked(rowBytes * framebuffer.Size.Height);
        byte[] pixels = new byte[contentLength];
        CopyPixels(framebuffer, pixels, rowBytes, ct);
        ConvertToBgra(framebuffer.Format, pixels);

        return new PreparedBitmapPixels(framebuffer.Size, rowBytes, pixels);
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
