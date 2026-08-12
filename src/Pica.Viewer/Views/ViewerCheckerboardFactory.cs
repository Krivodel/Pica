using System.Runtime.InteropServices;

using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Pica.Viewer.Views;

internal static class ViewerCheckerboardFactory
{
    internal const int TileSize = 10;

    private const int BytesPerPixel = 4;
    private const int SquareSize = TileSize / 2;
    private const double DefaultBitmapDpi = 96d;

    internal static ImageBrush CreateBrush(Bitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        return new ImageBrush
        {
            Source = bitmap,
            DestinationRect = new RelativeRect(
                0d,
                0d,
                TileSize,
                TileSize,
                RelativeUnit.Absolute),
            Stretch = Stretch.Fill,
            TileMode = TileMode.Tile
        };
    }

    internal static WriteableBitmap CreateBitmap(
        Color lightColor,
        Color darkColor)
    {
        PixelSize pixelSize = new(
            TileSize,
            TileSize);
        WriteableBitmap bitmap = new(
            pixelSize,
            new Vector(
                DefaultBitmapDpi,
                DefaultBitmapDpi),
            PixelFormat.Bgra8888,
            AlphaFormat.Opaque);
        byte[] pixels = CreatePixels(
            lightColor,
            darkColor);

        try
        {
            CopyPixels(bitmap, pixels);

            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    private static byte[] CreatePixels(
        Color lightColor,
        Color darkColor)
    {
        byte[] pixels = new byte[
            TileSize
            * TileSize
            * BytesPerPixel];

        for (int y = 0; y < TileSize; y++)
        {
            for (int x = 0; x < TileSize; x++)
            {
                bool isLightSquare =
                    (x < SquareSize)
                    == (y < SquareSize);
                Color color = isLightSquare
                    ? lightColor
                    : darkColor;
                int offset = checked(
                    ((y * TileSize) + x)
                    * BytesPerPixel);
                pixels[offset] = color.B;
                pixels[offset + 1] = color.G;
                pixels[offset + 2] = color.R;
                pixels[offset + 3] = color.A;
            }
        }

        return pixels;
    }

    private static void CopyPixels(
        WriteableBitmap bitmap,
        byte[] pixels)
    {
        using ILockedFramebuffer framebuffer = bitmap.Lock();
        int sourceRowBytes = checked(
            TileSize * BytesPerPixel);

        for (int row = 0; row < TileSize; row++)
        {
            IntPtr destinationAddress = IntPtr.Add(
                framebuffer.Address,
                row * framebuffer.RowBytes);
            Marshal.Copy(
                pixels,
                row * sourceRowBytes,
                destinationAddress,
                sourceRowBytes);
        }
    }
}
