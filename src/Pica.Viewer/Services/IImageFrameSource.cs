namespace Pica.Viewer.Services;

internal interface IImageFrameSource
{
    int FrameCount { get; }
    int LoadedFrameCount { get; }
    int PlaybackBufferFrameCount { get; }
    TimeSpan CurrentFrameDuration { get; }
    ImageFramePresentationModes FramePresentationMode { get; }
    uint AnimationIterations { get; }
    bool IsPlaybackStartBufferReady { get; }
    bool IsFullyDecoded { get; }
    bool IsDecodingComplete { get; }

    event EventHandler? FramesChanged;
    event EventHandler? FrameAvailabilityChanged;

    bool IsFrameAvailable(int frameIndex);
}
