using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;

namespace Pica.Viewer.Services;

internal sealed class SkiaProgressiveImageFrameReader :
    IProgressiveImageFrameReader
{
    public int FrameCount => _frameInformation.Length;
    public uint AnimationIterations
    {
        get
        {
            int repetitionCount = _codec.RepetitionCount;

            return repetitionCount < 0
                ? 0
                : checked((uint)repetitionCount + 1);
        }
    }

    private const int NoRequiredFrame = -1;

    private readonly SKManagedStream _managedStream;
    private readonly SKCodec _codec;
    private readonly SKCodecFrameInfo[] _frameInformation;
    private readonly SKImageInfo _decodeInformation;
    private readonly int[] _lastRequiredFrameUses;
    private readonly Dictionary<int, byte[]> _cachedFramePixels = [];
    private bool _disposed;

    internal SkiaProgressiveImageFrameReader(
        MemoryStream bufferedStream)
    {
        ArgumentNullException.ThrowIfNull(bufferedStream);
        _managedStream = new SKManagedStream(
            bufferedStream,
            true);

        try
        {
            SKCodec codec = SKCodec.Create(_managedStream)
                ?? throw new InvalidDataException(
                    "The Skia image decoder could not read the animated image.");

            try
            {
                _frameInformation = codec.FrameInfo;
                SKImageInfo sourceInformation = codec.Info;
                _decodeInformation = new SKImageInfo(
                    sourceInformation.Width,
                    sourceInformation.Height,
                    SKColorType.Bgra8888,
                    SKAlphaType.Premul);
                _lastRequiredFrameUses =
                    GetLastRequiredFrameUses(
                        _frameInformation);
                _codec = codec;
            }
            catch
            {
                codec.Dispose();
                throw;
            }
        }
        catch
        {
            _managedStream.Dispose();
            throw;
        }
    }

    public DecodedImageFrame ReadFrame(
        int frameIndex,
        CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if ((frameIndex < 0)
            || (frameIndex >= _frameInformation.Length))
        {
            throw new ArgumentOutOfRangeException(
                nameof(frameIndex),
                frameIndex,
                $"The frame index must be between 0 and {_frameInformation.Length - 1}.");
        }

        ct.ThrowIfCancellationRequested();
        using SKBitmap skiaBitmap = new(_decodeInformation);
        int requiredFrameIndex =
            _frameInformation[frameIndex].RequiredFrame;
        SKCodecOptions options = PrepareFrame(
            skiaBitmap,
            frameIndex,
            requiredFrameIndex,
            ct);
        SKCodecResult result = _codec.GetPixels(
            _decodeInformation,
            skiaBitmap.GetPixels(),
            skiaBitmap.RowBytes,
            options);

        if (result != SKCodecResult.Success)
        {
            throw new InvalidDataException(
                $"The Skia image decoder returned '{result}' for frame {frameIndex}.");
        }

        byte[] pixels = SkiaBitmapPixelBuffer.Read(
            skiaBitmap,
            ct);
        CacheFrameWhenRequired(
            frameIndex,
            requiredFrameIndex,
            pixels);
        Bitmap bitmap = BgraBitmapFactory.Create(
            new PixelSize(
                _decodeInformation.Width,
                _decodeInformation.Height),
            pixels,
            AlphaFormat.Premul,
            ct);
        TimeSpan duration =
            ImageAnimationTiming.NormalizeFrameDuration(
                _frameInformation[frameIndex].Duration);

        return new DecodedImageFrame(
            bitmap,
            duration);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cachedFramePixels.Clear();
        _codec.Dispose();
        _managedStream.Dispose();
    }

    private static int[] GetLastRequiredFrameUses(
        IReadOnlyList<SKCodecFrameInfo> frameInformation)
    {
        int[] lastRequiredFrameUses =
            Enumerable.Repeat(
                    NoRequiredFrame,
                    frameInformation.Count)
                .ToArray();

        for (int frameIndex = 0;
            frameIndex < frameInformation.Count;
            frameIndex++)
        {
            int requiredFrameIndex =
                frameInformation[frameIndex].RequiredFrame;

            if (requiredFrameIndex == NoRequiredFrame)
            {
                continue;
            }

            if ((requiredFrameIndex < 0)
                || (requiredFrameIndex >= frameIndex))
            {
                throw new InvalidDataException(
                    $"The Skia image decoder reported invalid required frame {requiredFrameIndex} for frame {frameIndex}.");
            }

            lastRequiredFrameUses[requiredFrameIndex] =
                frameIndex;
        }

        return lastRequiredFrameUses;
    }

    private SKCodecOptions PrepareFrame(
        SKBitmap bitmap,
        int frameIndex,
        int requiredFrameIndex,
        CancellationToken ct)
    {
        if ((requiredFrameIndex == NoRequiredFrame)
            || !_cachedFramePixels.TryGetValue(
                requiredFrameIndex,
                out byte[]? requiredFramePixels))
        {
            return new SKCodecOptions(frameIndex);
        }

        SkiaBitmapPixelBuffer.Write(
            requiredFramePixels,
            bitmap,
            ct);

        return new SKCodecOptions(
            frameIndex,
            requiredFrameIndex);
    }

    private void CacheFrameWhenRequired(
        int frameIndex,
        int requiredFrameIndex,
        byte[] pixels)
    {
        if (_lastRequiredFrameUses[frameIndex]
            != NoRequiredFrame)
        {
            _cachedFramePixels[frameIndex] =
                pixels;
        }

        if ((requiredFrameIndex != NoRequiredFrame)
            && (_lastRequiredFrameUses[requiredFrameIndex]
                == frameIndex))
        {
            _cachedFramePixels.Remove(
                requiredFrameIndex);
        }
    }
}
