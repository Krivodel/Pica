using System.Runtime.InteropServices;

using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Pica.Viewer.Services;

internal sealed class ImageChannelBitmapLoader
{
    private const int BytesPerPixel = 4;

    private static readonly SemaphoreSlim ChannelLoadLock = new(1, 1);

    private readonly IImageDecoderResolver _decoderResolver;

    public ImageChannelBitmapLoader(IImageDecoderResolver decoderResolver)
    {
        _decoderResolver = decoderResolver
            ?? throw new ArgumentNullException(nameof(decoderResolver));
    }

    public async Task<bool> ReadHasAlphaAsync(
        string fullPath,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);
        IImageDecoder decoder = _decoderResolver.Resolve(fullPath);

        return await RunLockedAsync(
            () => Task.Run(
                () =>
                {
                    using FileStream stream = File.OpenRead(fullPath);

                    return decoder.ReadHasAlpha(stream, ct);
                },
                ct),
            ct).ConfigureAwait(false);
    }

    public async Task<Bitmap> LoadAsync(
        Bitmap sourceBitmap,
        ImageChannel channel,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sourceBitmap);
        ArgumentNullException.ThrowIfNull(channel);

        return await RunLockedAsync(
            () => Task.Run(
                () =>
                {
                    PreparedBitmapPixels sourcePixels =
                        BitmapPixelReader.ReadUnpremultiplied(
                            sourceBitmap,
                            ct);

                    return CreateChannelBitmap(
                        sourcePixels,
                        sourceBitmap.Dpi,
                        channel,
                        ct);
                },
                ct),
            ct).ConfigureAwait(false);
    }

    internal static void ApplyChannel(
        PreparedBitmapPixels source,
        ImageChannel channel,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(channel);
        int rowLength = checked(source.PixelSize.Width * BytesPerPixel);

        for (int rowIndex = 0;
            rowIndex < source.PixelSize.Height;
            rowIndex++)
        {
            ct.ThrowIfCancellationRequested();
            int sourceRowOffset = rowIndex * source.RowBytes;

            for (int pixelOffset = sourceRowOffset;
                pixelOffset < sourceRowOffset + rowLength;
                pixelOffset += BytesPerPixel)
            {
                byte channelValue =
                    source.BgraPixels[pixelOffset + channel.BgraOffset];
                source.BgraPixels[pixelOffset] = channelValue;
                source.BgraPixels[pixelOffset + 1] = channelValue;
                source.BgraPixels[pixelOffset + 2] = channelValue;
                source.BgraPixels[pixelOffset + 3] = byte.MaxValue;
            }
        }
    }

    private static Bitmap CreateChannelBitmap(
        PreparedBitmapPixels sourcePixels,
        Vector dpi,
        ImageChannel channel,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ApplyChannel(
            sourcePixels,
            channel,
            ct);
        GCHandle pixelsHandle = GCHandle.Alloc(
            sourcePixels.BgraPixels,
            GCHandleType.Pinned);

        try
        {
            return new WriteableBitmap(
                PixelFormat.Bgra8888,
                AlphaFormat.Unpremul,
                pixelsHandle.AddrOfPinnedObject(),
                sourcePixels.PixelSize,
                dpi,
                sourcePixels.RowBytes);
        }
        finally
        {
            pixelsHandle.Free();
        }
    }

    private static async Task<TResult> RunLockedAsync<TResult>(
        Func<Task<TResult>> operation,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(operation);
        await ChannelLoadLock.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            return await operation().ConfigureAwait(false);
        }
        finally
        {
            ChannelLoadLock.Release();
        }
    }
}
