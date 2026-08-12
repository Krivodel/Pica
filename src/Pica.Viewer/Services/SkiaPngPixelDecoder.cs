using SkiaSharp;

namespace Pica.Viewer.Services;

internal static class SkiaPngPixelDecoder
{
    internal static byte[] Decode(
        byte[] content,
        int expectedWidth,
        int expectedHeight,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(content);
        ct.ThrowIfCancellationRequested();
        using MemoryStream sourceStream = new(
            content,
            false);
        using SKManagedStream managedStream = new(
            sourceStream);
        using SKCodec codec = SKCodec.Create(
            managedStream)
            ?? throw new InvalidDataException(
                "The Skia image decoder could not read the APNG frame.");

        if ((codec.Info.Width != expectedWidth)
            || (codec.Info.Height != expectedHeight))
        {
            throw new InvalidDataException(
                $"The APNG frame decoder returned {codec.Info.Width}x{codec.Info.Height}, expected {expectedWidth}x{expectedHeight}.");
        }

        SKImageInfo decodeInformation = new(
            expectedWidth,
            expectedHeight,
            SKColorType.Bgra8888,
            SKAlphaType.Premul);
        using SKBitmap bitmap = new(
            decodeInformation);
        SKCodecResult result = codec.GetPixels(
            decodeInformation,
            bitmap.GetPixels(),
            bitmap.RowBytes,
            new SKCodecOptions());

        if (result != SKCodecResult.Success)
        {
            throw new InvalidDataException(
                $"The Skia image decoder returned '{result}' for an APNG frame.");
        }

        return SkiaBitmapPixelBuffer.Read(
            bitmap,
            ct);
    }
}
