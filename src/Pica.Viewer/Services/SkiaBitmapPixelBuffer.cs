using System.Runtime.InteropServices;

using SkiaSharp;

namespace Pica.Viewer.Services;

internal static class SkiaBitmapPixelBuffer
{
    private const int BytesPerPixel = 4;

    internal static byte[] Read(
        SKBitmap bitmap,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        int rowLength = checked(
            bitmap.Width * BytesPerPixel);
        byte[] pixels = new byte[
            checked(rowLength * bitmap.Height)];
        IntPtr sourceAddress = bitmap.GetPixels();

        for (int rowIndex = 0;
            rowIndex < bitmap.Height;
            rowIndex++)
        {
            ct.ThrowIfCancellationRequested();
            Marshal.Copy(
                IntPtr.Add(
                    sourceAddress,
                    rowIndex * bitmap.RowBytes),
                pixels,
                rowIndex * rowLength,
                rowLength);
        }

        return pixels;
    }

    internal static void Write(
        byte[] source,
        SKBitmap bitmap,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(bitmap);
        int rowLength = checked(
            bitmap.Width * BytesPerPixel);
        int expectedLength = checked(
            rowLength * bitmap.Height);

        if (source.Length != expectedLength)
        {
            throw new InvalidDataException(
                $"The pixel buffer has length {source.Length}, expected {expectedLength}.");
        }

        IntPtr destinationAddress = bitmap.GetPixels();

        for (int rowIndex = 0;
            rowIndex < bitmap.Height;
            rowIndex++)
        {
            ct.ThrowIfCancellationRequested();
            Marshal.Copy(
                source,
                rowIndex * rowLength,
                IntPtr.Add(
                    destinationAddress,
                    rowIndex * bitmap.RowBytes),
                rowLength);
        }
    }
}
