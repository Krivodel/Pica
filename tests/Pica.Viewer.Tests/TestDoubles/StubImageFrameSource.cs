using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.TestDoubles;

internal sealed class StubImageFrameSource : IImageFrameSource
{
    public int FrameCount { get; private set; }
    public int LoadedFrameCount { get; private set; }
    public int PlaybackBufferFrameCount =>
        _playbackStartFrameCount;
    public TimeSpan CurrentFrameDuration { get; private set; }
    public ImageFramePresentationModes FramePresentationMode { get; private set; }
    public uint AnimationIterations { get; private set; }
    public bool IsPlaybackStartBufferReady { get; private set; }
    public bool IsFullyDecoded { get; private set; }
    public bool IsDecodingComplete { get; private set; }

    public event EventHandler? FramesChanged;
    public event EventHandler? FrameAvailabilityChanged;

    private bool[] _availableFrames = [];
    private int _playbackStartFrameCount;

    public bool IsFrameAvailable(int frameIndex)
    {
        return (frameIndex >= 0)
            && (frameIndex < _availableFrames.Length)
            && _availableFrames[frameIndex];
    }

    internal void SetFrames(
        int frameCount,
        TimeSpan currentFrameDuration,
        ImageFramePresentationModes framePresentationMode,
        uint animationIterations = 0)
    {
        FrameCount = frameCount;
        CurrentFrameDuration = currentFrameDuration;
        FramePresentationMode = framePresentationMode;
        AnimationIterations = animationIterations;
        _availableFrames = Enumerable
            .Repeat(true, frameCount)
            .ToArray();
        _playbackStartFrameCount = frameCount;
        UpdateDecodingState();
        FramesChanged?.Invoke(this, EventArgs.Empty);
    }

    internal void SetProgressiveFrames(
        int frameCount,
        int availableFrameCount,
        int playbackStartFrameCount,
        TimeSpan currentFrameDuration,
        ImageFramePresentationModes framePresentationMode,
        uint animationIterations = 0)
    {
        FrameCount = frameCount;
        CurrentFrameDuration = currentFrameDuration;
        FramePresentationMode = framePresentationMode;
        AnimationIterations = animationIterations;
        _availableFrames = Enumerable
            .Range(0, frameCount)
            .Select(frameIndex =>
                frameIndex < availableFrameCount)
            .ToArray();
        _playbackStartFrameCount =
            playbackStartFrameCount;
        UpdateDecodingState();
        FramesChanged?.Invoke(this, EventArgs.Empty);
    }

    internal void SetFrameAvailability(
        int frameIndex,
        bool isAvailable)
    {
        _availableFrames[frameIndex] = isAvailable;
        UpdateDecodingState();
        FrameAvailabilityChanged?.Invoke(
            this,
            EventArgs.Empty);
    }

    internal void CompleteDecodingWithMissingFrames()
    {
        IsDecodingComplete = true;
        FrameAvailabilityChanged?.Invoke(
            this,
            EventArgs.Empty);
    }

    private void UpdateDecodingState()
    {
        int availableFrameCount =
            _availableFrames.Count(isAvailable =>
                isAvailable);
        LoadedFrameCount = availableFrameCount;
        IsPlaybackStartBufferReady =
            availableFrameCount
            >= _playbackStartFrameCount;
        IsFullyDecoded =
            availableFrameCount
            == _availableFrames.Length;
        IsDecodingComplete = IsFullyDecoded;
    }
}
