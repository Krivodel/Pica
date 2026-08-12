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

    private readonly MemoryStream _bufferedStream;
    private readonly FrameRenderer _renderer;
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
        FrameRenderFrame frameInformation =
            _renderer[frameIndex];
        TimeSpan duration =
            ImageAnimationTiming.NormalizeFrameDuration(
                (frameInformation.End
                    - frameInformation.Begin)
                .TotalMilliseconds);
        Bitmap bitmap = BgraBitmapFactory.Create(
            new PixelSize(face.Width, face.Height),
            face.CopyPixels(),
            AlphaFormat.Premul,
            ct);

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
        ((IDisposable)_renderer).Dispose();
        _bufferedStream.Dispose();
    }
}
