using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Pica.Viewer.Services;

internal sealed class ApngProgressiveImageFrameReader :
    IProgressiveImageFrameReader
{
    public int FrameCount =>
        _animation.Frames.Count;
    public uint AnimationIterations =>
        _animation.AnimationIterations;
    public IReadOnlyList<TimeSpan> FrameDurations =>
        _frameDurations;

    private readonly ApngAnimationData _animation;
    private readonly ApngFrameCompositor _compositor;
    private readonly IReadOnlyList<TimeSpan> _frameDurations;
    private int _nextFrameIndex;
    private bool _disposed;

    internal ApngProgressiveImageFrameReader(
        ApngAnimationData animation)
    {
        _animation = animation
            ?? throw new ArgumentNullException(
                nameof(animation));
        _compositor = new ApngFrameCompositor(
            animation.CanvasWidth,
            animation.CanvasHeight);
        _frameDurations = Array.AsReadOnly(
            animation.Frames
                .Select(frame => frame.Duration)
                .ToArray());
    }

    public DecodedImageFrame ReadFrame(
        int frameIndex,
        CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if ((frameIndex < 0)
            || (frameIndex >= _animation.Frames.Count))
        {
            throw new ArgumentOutOfRangeException(
                nameof(frameIndex),
                frameIndex,
                $"The frame index must be between 0 and {_animation.Frames.Count - 1}.");
        }

        if (frameIndex != _nextFrameIndex)
        {
            _compositor.Reset();
            _nextFrameIndex = 0;
        }

        DecodedImageFrame? requestedFrame = null;

        for (int currentFrameIndex = _nextFrameIndex;
            currentFrameIndex <= frameIndex;
            currentFrameIndex++)
        {
            DecodedImageFrame decodedFrame = DecodeNextFrame(
                currentFrameIndex,
                ct);
            _nextFrameIndex = currentFrameIndex + 1;

            if (currentFrameIndex == frameIndex)
            {
                requestedFrame = decodedFrame;
                continue;
            }

            decodedFrame.Bitmap.Dispose();
        }

        return requestedFrame
            ?? throw new InvalidOperationException(
                $"APNG frame {frameIndex} was not decoded.");
    }

    public void Dispose()
    {
        _disposed = true;
    }

    private DecodedImageFrame DecodeNextFrame(
        int frameIndex,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ApngFrameData frame =
            _animation.Frames[frameIndex];
        byte[] pngContent =
            ApngFramePngBuilder.Build(
                _animation,
                frame);
        byte[] framePixels =
            SkiaPngPixelDecoder.Decode(
                pngContent,
                frame.Width,
                frame.Height,
                ct);
        byte[] canvasPixels =
            _compositor.Compose(
                frameIndex,
                frame,
                framePixels,
                ct);
        Bitmap bitmap = BgraBitmapFactory.Create(
            new PixelSize(
                _animation.CanvasWidth,
                _animation.CanvasHeight),
            canvasPixels,
            AlphaFormat.Premul,
            ct);

        return new DecodedImageFrame(
            bitmap,
            frame.Duration);
    }
}
