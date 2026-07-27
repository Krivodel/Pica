using System.Runtime.InteropServices;

using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;

namespace Pica.Viewer.Services;

internal sealed class PngImageEncoder
{
    private const int FastCompressionLevel = 1;

    private static readonly SKPngEncoderOptions EncoderOptions = new(
        SKPngEncoderFilterFlags.None,
        FastCompressionLevel);

    public async Task<byte[]> EncodeAsync(Bitmap bitmap, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        return await Task.Run(() => Encode(bitmap, ct), ct).ConfigureAwait(false);
    }

    internal static byte[] EncodePixels(
        PreparedClipboardBitmap bitmap,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        GCHandle pixelsHandle = GCHandle.Alloc(bitmap.BgraPixels, GCHandleType.Pinned);

        try
        {
            return EncodePixels(
                bitmap.PixelSize,
                PixelFormat.Bgra8888,
                AlphaFormat.Premul,
                pixelsHandle.AddrOfPinnedObject(),
                bitmap.RowBytes,
                ct);
        }
        finally
        {
            pixelsHandle.Free();
        }
    }

    internal static byte[] EncodePixels(
        PixelSize pixelSize,
        PixelFormat pixelFormat,
        AlphaFormat alphaFormat,
        IntPtr address,
        int rowBytes,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        SKImageInfo imageInfo = new(
            pixelSize.Width,
            pixelSize.Height,
            GetColorType(pixelFormat),
            GetAlphaType(alphaFormat));
        using SKPixmap pixmap = new(imageInfo, address, rowBytes);
        using SKData data = pixmap.Encode(EncoderOptions)
            ?? throw new InvalidOperationException("Failed to encode the selected area as PNG.");
        ct.ThrowIfCancellationRequested();

        return data.ToArray();
    }

    private static byte[] Encode(Bitmap bitmap, CancellationToken ct)
    {
        if (bitmap is WriteableBitmap writeableBitmap)
        {
            return EncodeWriteableBitmap(writeableBitmap, ct);
        }

        using WriteableBitmap readableBitmap = BitmapPixelCopy.CreateCopy(bitmap);

        return EncodeWriteableBitmap(readableBitmap, ct);
    }

    private static byte[] EncodeWriteableBitmap(
        WriteableBitmap bitmap,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using ILockedFramebuffer framebuffer = bitmap.Lock();

        return EncodePixels(
            framebuffer.Size,
            framebuffer.Format,
            framebuffer.AlphaFormat,
            framebuffer.Address,
            framebuffer.RowBytes,
            ct);
    }

    private static SKColorType GetColorType(PixelFormat pixelFormat)
    {
        if (pixelFormat == PixelFormat.Bgra8888)
        {
            return SKColorType.Bgra8888;
        }

        if (pixelFormat == PixelFormat.Rgba8888)
        {
            return SKColorType.Rgba8888;
        }

        throw new NotSupportedException($"Unsupported pixel format: {pixelFormat}.");
    }

    private static SKAlphaType GetAlphaType(AlphaFormat alphaFormat)
    {
        if (alphaFormat == AlphaFormat.Premul)
        {
            return SKAlphaType.Premul;
        }

        if (alphaFormat == AlphaFormat.Unpremul)
        {
            return SKAlphaType.Unpremul;
        }

        if (alphaFormat == AlphaFormat.Opaque)
        {
            return SKAlphaType.Opaque;
        }

        throw new NotSupportedException($"Unsupported alpha format: {alphaFormat}.");
    }
}
