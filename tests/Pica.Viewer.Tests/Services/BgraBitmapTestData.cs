using System.Runtime.InteropServices;

using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Pica.Viewer.Tests.Services;

internal static class BgraBitmapTestData
{
    public const int Width = 2;
    public const int Height = 2;
    public const int RowBytes = 8;

    public static PixelSize PixelSize => new(Width, Height);
    public static byte[] Pixels => (byte[])PixelsValue.Clone();

    private static readonly byte[] PixelsValue =
    [
        11, 21, 32, 255, 12, 21, 33, 255,
        11, 22, 33, 255, 12, 22, 34, 255
    ];

    public static Bitmap CreateBitmap()
    {
        WriteableBitmap bitmap = new(
            PixelSize,
            new Vector(96d, 96d),
            PixelFormat.Bgra8888,
            AlphaFormat.Unpremul);
        using ILockedFramebuffer framebuffer = bitmap.Lock();
        byte[] pixels = Pixels;
        Marshal.Copy(
            pixels,
            0,
            framebuffer.Address,
            pixels.Length);

        return bitmap;
    }
}
