using Avalonia.Media.Imaging;
using ImageMagick;

namespace Pica.Viewer.Services;

internal sealed class MagickProgressiveImageFrameReader :
    IProgressiveImageFrameReader
{
    public int FrameCount => _frameDurations.Count;
    public uint AnimationIterations { get; }

    private readonly MemoryStream _bufferedStream;
    private readonly IReadOnlyList<TimeSpan> _frameDurations;
    private readonly MagickFormat? _readFormat;
    private readonly ImageAnimationBufferingPolicy _bufferingPolicy;
    private readonly MagickAnimationDecodingPolicy _decodingPolicy;
    private readonly int _initialBatchFrameCount;
    private MagickImageCollection? _frameBatch;
    private int _frameBatchStartIndex;
    private bool _disposed;

    internal MagickProgressiveImageFrameReader(
        MemoryStream bufferedStream,
        IReadOnlyList<TimeSpan> frameDurations,
        uint animationIterations,
        MagickFormat? readFormat,
        ImageAnimationBufferingPolicy bufferingPolicy,
        MagickAnimationDecodingPolicy decodingPolicy)
    {
        _bufferedStream = bufferedStream
            ?? throw new ArgumentNullException(nameof(bufferedStream));
        _frameDurations = frameDurations
            ?? throw new ArgumentNullException(nameof(frameDurations));
        _bufferingPolicy = bufferingPolicy
            ?? throw new ArgumentNullException(
                nameof(bufferingPolicy));
        _decodingPolicy = decodingPolicy
            ?? throw new ArgumentNullException(
                nameof(decodingPolicy));
        AnimationIterations = animationIterations;
        _readFormat = readFormat;
        _initialBatchFrameCount =
            _decodingPolicy.GetInitialDecodeFrameCount(
                _frameDurations,
                _bufferingPolicy.GetRequiredFrameCount(
                    FrameCount));
    }

    public DecodedImageFrame ReadFrame(
        int frameIndex,
        CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if ((frameIndex < 0)
            || (frameIndex >= _frameDurations.Count))
        {
            throw new ArgumentOutOfRangeException(
                nameof(frameIndex),
                frameIndex,
                $"The frame index must be between 0 and {_frameDurations.Count - 1}.");
        }

        ct.ThrowIfCancellationRequested();
        EnsureFrameBatch(frameIndex, ct);
        MagickImageCollection frameBatch =
            _frameBatch
            ?? throw new InvalidOperationException(
                "The animation frame batch is not available.");
        IMagickImage<byte> image =
            frameBatch[
                frameIndex
                - _frameBatchStartIndex];

        try
        {
            return CreateFrame(
                image,
                frameIndex,
                ct);
        }
        finally
        {
            ReleaseConsumedFrame(
                frameBatch,
                image,
                frameIndex);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _frameBatch?.Dispose();
        _frameBatch = null;
        _bufferedStream.Dispose();
    }

    private DecodedImageFrame CreateFrame(
        IMagickImage<byte> image,
        int frameIndex,
        CancellationToken ct)
    {
        image.AutoOrient();
        Bitmap bitmap = MagickImageDecoder.CreateBitmap(
            image,
            ct);

        return new DecodedImageFrame(
            bitmap,
            _frameDurations[frameIndex]);
    }

    private MagickImageCollection ReadFrameRange(
        int firstFrameIndex,
        int frameCount,
        CancellationToken ct)
    {
        _bufferedStream.Position = 0;
        MagickReadSettings readSettings = new()
        {
            FrameIndex = checked(
                (uint)firstFrameIndex),
            FrameCount = checked(
                (uint)frameCount)
        };

        if (_readFormat is MagickFormat format)
        {
            readSettings.Format = format;
        }

        MagickImageCollection images = new();

        try
        {
            MagickImageCollectionReader.Read(
                images,
                _bufferedStream,
                readSettings);
            ct.ThrowIfCancellationRequested();

            if (images.Count != frameCount)
            {
                throw new InvalidDataException(
                    $"The image decoder returned {images.Count} images for the frame range beginning at {firstFrameIndex}, expected {frameCount}.");
            }

            return images;
        }
        catch
        {
            images.Dispose();
            throw;
        }
    }

    private void EnsureFrameBatch(
        int frameIndex,
        CancellationToken ct)
    {
        MagickImageCollection? currentBatch =
            _frameBatch;
        int currentBatchEndIndex =
            _frameBatchStartIndex
            + (currentBatch?.Count ?? 0);

        if ((currentBatch is not null)
            && (frameIndex >= _frameBatchStartIndex)
            && (frameIndex < currentBatchEndIndex))
        {
            return;
        }

        int batchFrameCount = frameIndex == 0
            ? _initialBatchFrameCount
            : FrameCount - frameIndex;
        MagickImageCollection nextBatch =
            ReadFrameRange(
                frameIndex,
                batchFrameCount,
                ct);
        _frameBatch = nextBatch;
        _frameBatchStartIndex = frameIndex;
        currentBatch?.Dispose();
    }

    private void ReleaseConsumedFrame(
        MagickImageCollection frameBatch,
        IMagickImage<byte> image,
        int frameIndex)
    {
        if (frameIndex != _frameBatchStartIndex)
        {
            return;
        }

        frameBatch.RemoveAt(0);
        image.Dispose();
        _frameBatchStartIndex++;
    }
}
