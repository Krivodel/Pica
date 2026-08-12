using Avalonia.Media.Imaging;

namespace Pica.Viewer.Services;

internal sealed class DecodedImage : IDisposable
{
    internal IReadOnlyList<DecodedImageFrame> Frames =>
        GetLoadedFrames();
    internal int FrameCount => _frames.Length;
    internal int LoadedFrameCount
    {
        get
        {
            lock (_sync)
            {
                return _loadedFrameCount;
            }
        }
    }
    internal int PreferredInitialFrameIndex { get; }
    internal int PlaybackStartFrameCount { get; }
    internal int MaximumResidentFrameCount =>
        RetainsCompleteAnimation
            ? FrameCount
            : Math.Min(
                FrameCount,
                PlaybackStartFrameCount
                    + Math.Min(
                        2,
                        FrameCount
                        - PlaybackStartFrameCount));
    internal ImageFramePresentationModes FramePresentationMode { get; }
    internal ImageFrameNumbering FrameNumbering { get; }
    internal uint AnimationIterations { get; }
    internal bool RetainsCompleteAnimation
    {
        get
        {
            lock (_sync)
            {
                return !_usesBoundedFrameCache;
            }
        }
    }
    internal bool IsPlaybackStartBufferReady
    {
        get
        {
            lock (_sync)
            {
                return IsPlaybackBufferReadyCore();
            }
        }
    }
    internal bool IsFullyDecoded
    {
        get
        {
            lock (_sync)
            {
                return _loadedFrameCount
                    == _frames.Length;
            }
        }
    }
    internal bool IsDecodingComplete
    {
        get
        {
            lock (_sync)
            {
                return _decodingCompletion.IsCompleted;
            }
        }
    }
    internal Task<Exception?> DecodingCompletion
    {
        get
        {
            lock (_sync)
            {
                return _decodingCompletion;
            }
        }
    }

    internal event EventHandler<DecodedImageFrameDecodedEventArgs>? FrameDecoded;

    private static readonly Task<Exception?> CompletedDecoding =
        Task.FromResult<Exception?>(null);

    private readonly object _sync = new();
    private readonly DecodedImageFrame?[] _frames;
    private readonly SemaphoreSlim _frameRequestSignal =
        new(0, 1);
    private readonly ImageAnimationFrameCachePolicy _frameCachePolicy;
    private bool _usesBoundedFrameCache;
    private OperationCancellation? _decodingCancellation;
    private Task<Exception?> _decodingCompletion =
        CompletedDecoding;
    private Action<Bitmap> _releaseStoredBitmap =
        DisposeBitmap;
    private int _loadedFrameCount;
    private int _playbackFrameIndex;
    private bool _isFrameRequestPending;
    private bool _disposed;

    internal DecodedImage(
        IReadOnlyList<DecodedImageFrame> frames,
        ImageFramePresentationModes framePresentationMode,
        uint animationIterations)
        : this(
            frames,
            framePresentationMode,
            animationIterations,
            0,
            ImageFrameNumbering.Forward)
    {
    }

    internal DecodedImage(
        IReadOnlyList<DecodedImageFrame> frames,
        ImageFramePresentationModes framePresentationMode,
        uint animationIterations,
        int preferredInitialFrameIndex)
        : this(
            frames,
            framePresentationMode,
            animationIterations,
            preferredInitialFrameIndex,
            ImageFrameNumbering.Forward)
    {
    }

    internal DecodedImage(
        IReadOnlyList<DecodedImageFrame> frames,
        ImageFramePresentationModes framePresentationMode,
        uint animationIterations,
        int preferredInitialFrameIndex,
        ImageFrameNumbering frameNumbering)
    {
        ArgumentNullException.ThrowIfNull(frames);

        if (frames.Count == 0)
        {
            throw new ArgumentException(
                "A decoded image must contain at least one frame.",
                nameof(frames));
        }

        ValidateFrameIndex(
            preferredInitialFrameIndex,
            frames.Count,
            nameof(preferredInitialFrameIndex));
        _frames = new DecodedImageFrame?[frames.Count];

        for (int i = 0; i < frames.Count; i++)
        {
            _frames[i] = frames[i];
        }

        _loadedFrameCount = frames.Count;
        _frameCachePolicy =
            ImageAnimationFrameCachePolicy.Default;
        PreferredInitialFrameIndex = preferredInitialFrameIndex;
        PlaybackStartFrameCount = frames.Count;
        FramePresentationMode = frames.Count > 1
            ? framePresentationMode
            : ImageFramePresentationModes.None;
        FrameNumbering = frameNumbering;
        AnimationIterations = animationIterations;
    }

    private DecodedImage(
        int frameCount,
        ImageFramePresentationModes framePresentationMode,
        uint animationIterations,
        int playbackStartFrameCount,
        ImageAnimationFrameCachePolicy frameCachePolicy)
    {
        if (frameCount <= 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(frameCount),
                frameCount,
                "A progressively decoded image must contain multiple frames.");
        }

        if ((playbackStartFrameCount <= 0)
            || (playbackStartFrameCount > frameCount))
        {
            throw new ArgumentOutOfRangeException(
                nameof(playbackStartFrameCount),
                playbackStartFrameCount,
                $"The playback start frame count must be between 1 and {frameCount}.");
        }

        _frameCachePolicy = frameCachePolicy
            ?? throw new ArgumentNullException(
                nameof(frameCachePolicy));
        _frames = new DecodedImageFrame?[frameCount];
        PreferredInitialFrameIndex = 0;
        PlaybackStartFrameCount = playbackStartFrameCount;
        FramePresentationMode = framePresentationMode;
        FrameNumbering = ImageFrameNumbering.Forward;
        AnimationIterations = animationIterations;
    }

    public void Dispose()
    {
        OperationCancellation? cancellation;
        IReadOnlyList<DecodedImageFrame> frames;

        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            cancellation = _decodingCancellation;
            frames = GetLoadedFramesCore();

            for (int i = 0; i < _frames.Length; i++)
            {
                _frames[i] = null;
            }

            _loadedFrameCount = 0;
        }

        cancellation?.Cancel();
        SignalFrameRequest();

        foreach (DecodedImageFrame frame in frames)
        {
            ReleaseStoredBitmap(frame.Bitmap);
        }
    }

    internal static DecodedImage CreateProgressive(
        int frameCount,
        ImageFramePresentationModes framePresentationMode,
        uint animationIterations,
        int playbackStartFrameCount,
        ImageAnimationFrameCachePolicy? frameCachePolicy = null)
    {
        return new DecodedImage(
            frameCount,
            framePresentationMode,
            animationIterations,
            playbackStartFrameCount,
            frameCachePolicy
                ?? ImageAnimationFrameCachePolicy.Default);
    }

    internal static DecodedImage CreateSingle(Bitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        List<DecodedImageFrame> frames =
        [
            new DecodedImageFrame(bitmap, TimeSpan.Zero)
        ];

        return new DecodedImage(
            frames,
            ImageFramePresentationModes.None,
            0);
    }

    internal DecodedImageFrame? GetFrame(int frameIndex)
    {
        if ((frameIndex < 0)
            || (frameIndex >= _frames.Length))
        {
            return null;
        }

        lock (_sync)
        {
            return _frames[frameIndex];
        }
    }

    internal bool IsFrameAvailable(int frameIndex)
    {
        return GetFrame(frameIndex) is not null;
    }

    internal void SetPlaybackFrameIndex(int frameIndex)
    {
        ValidateFrameIndex(
            frameIndex,
            _frames.Length,
            nameof(frameIndex));

        if (!_usesBoundedFrameCache)
        {
            return;
        }

        List<DecodedImageFrame> framesToDispose = [];

        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _playbackFrameIndex = frameIndex;

            for (int i = 0; i < _frames.Length; i++)
            {
                DecodedImageFrame? frame = _frames[i];

                if ((frame is null)
                    || ShouldRetainFrameCore(i))
                {
                    continue;
                }

                _frames[i] = null;
                _loadedFrameCount--;
                framesToDispose.Add(frame);
            }
        }

        foreach (DecodedImageFrame frame in framesToDispose)
        {
            ReleaseStoredBitmap(frame.Bitmap);
        }

        SignalFrameRequest();
    }

    internal void AddFrame(
        int frameIndex,
        DecodedImageFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ValidateFrameIndex(
            frameIndex,
            _frames.Length,
            nameof(frameIndex));
        bool shouldDispose;
        bool wasAdded = false;

        lock (_sync)
        {
            if ((_loadedFrameCount == 0)
                && !_frameCachePolicy.CanRetainAnimation(
                    FrameCount,
                    frame.Bitmap.PixelSize))
            {
                _usesBoundedFrameCache = true;
            }

            shouldDispose = _disposed
                || !ShouldRetainFrameCore(frameIndex);

            if (!shouldDispose)
            {
                if (_frames[frameIndex] is not null)
                {
                    frame.Bitmap.Dispose();

                    throw new InvalidOperationException(
                        $"Image frame {frameIndex} has already been decoded.");
                }

                _frames[frameIndex] = frame;
                _loadedFrameCount++;
                wasAdded = true;
            }
        }

        if (shouldDispose)
        {
            frame.Bitmap.Dispose();
            return;
        }

        if (wasAdded)
        {
            FrameDecoded?.Invoke(
                this,
                new DecodedImageFrameDecodedEventArgs(frameIndex));
        }
    }

    internal void StartDecoding(
        Action<CancellationToken> decodeRemainingFrames)
    {
        ArgumentNullException.ThrowIfNull(decodeRemainingFrames);

        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_decodingCancellation is not null)
            {
                throw new InvalidOperationException(
                    "Progressive image decoding has already started.");
            }

            OperationCancellation cancellation = new();
            _decodingCancellation = cancellation;
            _decodingCompletion = Task.Run(
                () => RunDecoding(
                    decodeRemainingFrames,
                    cancellation));
        }
    }

    internal void SetStoredBitmapReleaseHandler(
        Action<Bitmap> releaseStoredBitmap)
    {
        ArgumentNullException.ThrowIfNull(releaseStoredBitmap);

        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _releaseStoredBitmap = releaseStoredBitmap;
        }
    }

    internal void CancelDecoding()
    {
        OperationCancellation? cancellation;

        lock (_sync)
        {
            cancellation = _decodingCancellation;
        }

        cancellation?.Cancel();
        SignalFrameRequest();
    }

    internal int? GetNextRequestedFrameIndex()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return null;
            }

            for (int distance = 0;
                distance < PlaybackStartFrameCount;
                distance++)
            {
                int frameIndex = GetForwardFrameIndexCore(
                    distance);

                if (_frames[frameIndex] is null)
                {
                    return frameIndex;
                }
            }

            return null;
        }
    }

    internal void WaitForFrameRequest(CancellationToken ct)
    {
        _frameRequestSignal.Wait(ct);

        lock (_sync)
        {
            _isFrameRequestPending = false;
        }
    }

    private static void ValidateFrameIndex(
        int frameIndex,
        int frameCount,
        string parameterName)
    {
        if ((frameIndex < 0)
            || (frameIndex >= frameCount))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                frameIndex,
                $"The frame index must be between 0 and {frameCount - 1}.");
        }
    }

    private static void DisposeBitmap(Bitmap bitmap)
    {
        bitmap.Dispose();
    }

    private static Exception? RunDecoding(
        Action<CancellationToken> decodeRemainingFrames,
        OperationCancellation cancellation)
    {
        try
        {
            decodeRemainingFrames(cancellation.Token);

            return null;
        }
        catch (OperationCanceledException)
            when (cancellation.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
        finally
        {
            cancellation.Complete();
        }
    }

    private bool IsPlaybackBufferReadyCore()
    {
        if (!_usesBoundedFrameCache)
        {
            return _loadedFrameCount
                >= PlaybackStartFrameCount;
        }

        for (int distance = 0;
            distance < PlaybackStartFrameCount;
            distance++)
        {
            int frameIndex = GetForwardFrameIndexCore(
                distance);

            if (_frames[frameIndex] is null)
            {
                return false;
            }
        }

        return true;
    }

    private bool ShouldRetainFrameCore(int frameIndex)
    {
        if (!_usesBoundedFrameCache)
        {
            return true;
        }

        if (frameIndex == PreferredInitialFrameIndex)
        {
            return true;
        }

        int previousFrameIndex =
            (_playbackFrameIndex - 1 + _frames.Length)
            % _frames.Length;

        if (frameIndex == previousFrameIndex)
        {
            return true;
        }

        int forwardDistance =
            (frameIndex - _playbackFrameIndex + _frames.Length)
            % _frames.Length;

        return forwardDistance < PlaybackStartFrameCount;
    }

    private int GetForwardFrameIndexCore(int distance)
    {
        return (_playbackFrameIndex + distance)
            % _frames.Length;
    }

    private void SignalFrameRequest()
    {
        lock (_sync)
        {
            if (_isFrameRequestPending)
            {
                return;
            }

            _isFrameRequestPending = true;
        }

        _frameRequestSignal.Release();
    }

    private void ReleaseStoredBitmap(Bitmap bitmap)
    {
        Action<Bitmap> releaseStoredBitmap;

        lock (_sync)
        {
            releaseStoredBitmap = _releaseStoredBitmap;
        }

        releaseStoredBitmap(bitmap);
    }

    private IReadOnlyList<DecodedImageFrame> GetLoadedFrames()
    {
        lock (_sync)
        {
            return GetLoadedFramesCore();
        }
    }

    private IReadOnlyList<DecodedImageFrame> GetLoadedFramesCore()
    {
        List<DecodedImageFrame> frames =
            new(_loadedFrameCount);

        foreach (DecodedImageFrame? frame in _frames)
        {
            if (frame is not null)
            {
                frames.Add(frame);
            }
        }

        return frames.AsReadOnly();
    }
}
