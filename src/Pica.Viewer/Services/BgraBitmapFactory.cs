using System.Runtime.InteropServices;

using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Pica.Viewer.Services;

internal static class BgraBitmapFactory
{
    private const int BytesPerPixel = 4;
    private const double DefaultDpi = 96d;

    internal static Bitmap Create(
        PixelSize pixelSize,
        byte[] source,
        AlphaFormat alphaFormat,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(source);
        ct.ThrowIfCancellationRequested();
        WriteableBitmap bitmap = new(
            pixelSize,
            new Vector(DefaultDpi, DefaultDpi),
            PixelFormat.Bgra8888,
            alphaFormat);

        try
        {
            CopyPixels(bitmap, source, ct);

            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    private static void CopyPixels(
        WriteableBitmap bitmap,
        byte[] source,
        CancellationToken ct)
    {
        int sourceRowBytes = checked(
            bitmap.PixelSize.Width * BytesPerPixel);
        int expectedLength = checked(
            sourceRowBytes * bitmap.PixelSize.Height);

        if (source.Length != expectedLength)
        {
            throw new InvalidDataException(
                $"The decoded image pixel buffer has length {source.Length}, expected {expectedLength}.");
        }

        using ILockedFramebuffer framebuffer = bitmap.Lock();

        if (framebuffer.RowBytes == sourceRowBytes)
        {
            Marshal.Copy(
                source,
                0,
                framebuffer.Address,
                source.Length);
            ct.ThrowIfCancellationRequested();
            return;
        }

        for (int row = 0; row < framebuffer.Size.Height; row++)
        {
            ct.ThrowIfCancellationRequested();
            IntPtr destinationAddress = IntPtr.Add(
                framebuffer.Address,
                row * framebuffer.RowBytes);
            Marshal.Copy(
                source,
                row * sourceRowBytes,
                destinationAddress,
                sourceRowBytes);
        }
    }
}
