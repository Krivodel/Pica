using System.Collections.Concurrent;

using Avalonia.Media.Imaging;

using Pica.Viewer.Services;
using Pica.Viewer.Tests.Services;

namespace Pica.Viewer.Tests.TestDoubles;

internal sealed class ControlledProgressiveImageFrameReader :
    IProgressiveImageFrameReader
{
    public int FrameCount { get; }
    public uint AnimationIterations => 0;

    internal Task RemainingFrameRequested =>
        _remainingFrameRequested.Task;
    internal bool IsDisposed { get; private set; }
    internal int ReadFrameCount => Volatile.Read(ref _readFrameCount);
    internal IReadOnlyList<int> ReadFrameIndices =>
        _readFrameIndices.ToArray();

    private readonly ManualResetEventSlim _remainingFramesRelease =
        new(false);
    private readonly TaskCompletionSource _remainingFrameRequested =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ConcurrentQueue<int> _readFrameIndices =
        new ConcurrentQueue<int>();
    private int _readFrameCount;

    internal ControlledProgressiveImageFrameReader(int frameCount)
    {
        FrameCount = frameCount;
    }

    public DecodedImageFrame ReadFrame(
        int frameIndex,
        CancellationToken ct)
    {
        Interlocked.Increment(ref _readFrameCount);
        _readFrameIndices.Enqueue(frameIndex);

        if (frameIndex >= 2)
        {
            _remainingFrameRequested.TrySetResult();
            _remainingFramesRelease.Wait(ct);
        }

        Bitmap bitmap = BgraBitmapTestData.CreateBitmap();

        return new DecodedImageFrame(
            bitmap,
            TimeSpan.FromMilliseconds(50d));
    }

    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        IsDisposed = true;
        _remainingFramesRelease.Dispose();
    }

    internal void ReleaseRemainingFrames()
    {
        _remainingFramesRelease.Set();
    }
}
