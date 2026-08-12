using AnimatedImage;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Pica.Viewer.Services;

internal sealed class AnimatedImageProgressiveFrameReader :
    IProgressiveImageFrameReader
{
    public int FrameCount => _renderer.FrameCount;
    public uint AnimationIterations =>
        _renderer.RepeatCount <= 0
            ? 0
            : checked((uint)_renderer.RepeatCount);
    public IReadOnlyList<TimeSpan> FrameDurations =>
        _frameDurations;

    private readonly MemoryStream _bufferedStream;
    private readonly FrameRenderer _renderer;
    private readonly IReadOnlyList<TimeSpan> _frameDurations;
    private bool _disposed;

    internal AnimatedImageProgressiveFrameReader(
        MemoryStream bufferedStream)
    {
        _bufferedStream = bufferedStream
            ?? throw new ArgumentNullException(nameof(bufferedStream));

        try
        {
            _renderer = FrameRenderer.Create(
                bufferedStream,
                new AnimatedImageBitmapFaceFactory());
            _frameDurations = Array.AsReadOnly(
                Enumerable
                    .Range(0, _renderer.FrameCount)
                    .Select(GetFrameDuration)
                    .ToArray());
        }
        catch
        {
            bufferedStream.Dispose();
            throw;
        }
    }

    public DecodedImageFrame ReadFrame(
        int frameIndex,
        CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ct.ThrowIfCancellationRequested();
        _renderer.ProcessFrame(frameIndex);
        AnimatedImageBitmapFace face =
            _renderer.Current as AnimatedImageBitmapFace
            ?? throw new InvalidDataException(
                "The animated image decoder returned an unexpected pixel surface.");
        Bitmap bitmap = BgraBitmapFactory.Create(
            new PixelSize(face.Width, face.Height),
            face.CopyPixels(),
            AlphaFormat.Premul,
            ct);

        return new DecodedImageFrame(
            bitmap,
            _frameDurations[frameIndex]);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ((IDisposable)_renderer).Dispose();
        _bufferedStream.Dispose();
    }

    private TimeSpan GetFrameDuration(int frameIndex)
    {
        FrameRenderFrame frameInformation =
            _renderer[frameIndex];

        return ImageAnimationTiming.NormalizeFrameDuration(
            (frameInformation.End
                - frameInformation.Begin)
            .TotalMilliseconds);
    }
}
