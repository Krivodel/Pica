using System.ComponentModel;

using Microsoft.Extensions.Logging;

namespace Pica.Viewer.Services;

internal sealed class ImageAnimationPlaybackController :
    IDisposable
{
    internal static TimeSpan InitialBufferingIndicatorDelay { get; } =
        TimeSpan.FromMilliseconds(500d);
    internal ImageAnimationPlaybackState State =>
        (ImageAnimationPlaybackState)Volatile.Read(
            ref _stateValue);

    private readonly ImageViewerSession _session;
    private readonly IImageFrameSource _frameSource;
    private readonly IImageAnimationDelayScheduler _delayScheduler;
    private readonly IViewerUiDispatcher _uiDispatcher;
    private readonly ILogger<ImageAnimationPlaybackController> _logger;
    private readonly object _disposalSync = new();
    private OperationCancellation? _playbackCancellation;
    private OperationCancellation? _bufferingIndicatorCancellation;
    private Task? _playbackTask;
    private Task? _bufferingIndicatorTask;
    private Task? _disposalTask;
    private uint _completedIterations;
    private int _stateValue;
    private bool _disposed;

    internal ImageAnimationPlaybackController(
        ImageViewerSession session,
        IImageFrameSource frameSource,
        IImageAnimationDelayScheduler delayScheduler,
        IViewerUiDispatcher uiDispatcher,
        ILogger<ImageAnimationPlaybackController> logger)
    {
        _session = session
            ?? throw new ArgumentNullException(nameof(session));
        _frameSource = frameSource
            ?? throw new ArgumentNullException(nameof(frameSource));
        _delayScheduler = delayScheduler
            ?? throw new ArgumentNullException(nameof(delayScheduler));
        _uiDispatcher = uiDispatcher
            ?? throw new ArgumentNullException(nameof(uiDispatcher));
        _logger = logger
            ?? throw new ArgumentNullException(nameof(logger));
        _session.PropertyChanged += OnSessionPropertyChanged;
        _frameSource.FramesChanged += OnFramesChanged;
        _frameSource.FrameAvailabilityChanged +=
            OnFrameAvailabilityChanged;
    }

    public void Dispose()
    {
        Task disposalTask = BeginDisposal();

        if (!disposalTask.IsCompletedSuccessfully)
        {
            _ = ObserveDisposalAsync(disposalTask);
        }
    }

    internal async Task DisposeAsync(CancellationToken ct)
    {
        Task disposalTask = BeginDisposal();

        await disposalTask
            .WaitAsync(ct)
            .ConfigureAwait(false);
    }

    private Task BeginDisposal()
    {
        lock (_disposalSync)
        {
            if (_disposalTask is not null)
            {
                return _disposalTask;
            }

            _disposed = true;
            _session.PropertyChanged -= OnSessionPropertyChanged;
            _frameSource.FramesChanged -= OnFramesChanged;
            _frameSource.FrameAvailabilityChanged -=
                OnFrameAvailabilityChanged;
            List<Task> pendingTasks = [];

            if (_playbackTask is not null)
            {
                pendingTasks.Add(_playbackTask);
            }

            if (_bufferingIndicatorTask is not null)
            {
                pendingTasks.Add(_bufferingIndicatorTask);
            }

            CancelScheduledAdvance();
            CancelBufferingIndicatorDelay();
            _session.SetAnimationBuffering(false);
            SetState(ImageAnimationPlaybackState.Idle);
            _disposalTask = pendingTasks.Count == 0
                ? Task.CompletedTask
                : Task.WhenAll(pendingTasks);

            return _disposalTask;
        }
    }

    private void ConfigurePlayback()
    {
        CancelScheduledAdvance();
        CancelBufferingIndicatorDelay();
        _session.SetAnimationBuffering(false);

        if (!CanPlay())
        {
            SetState(ImageAnimationPlaybackState.Idle);
            return;
        }

        if (_frameSource.IsPlaybackStartBufferReady)
        {
            StartPlaying();
            return;
        }

        if (_frameSource.IsDecodingComplete)
        {
            SetState(ImageAnimationPlaybackState.Failed);
            return;
        }

        EnterInitialBuffering();
    }

    private bool CanPlay()
    {
        return !_disposed
            && _session.IsAnimationPlaybackEnabled
            && (_frameSource.FrameCount > 1)
            && !HasCompletedPlayback();
    }

    private void EnterInitialBuffering()
    {
        SetState(
            ImageAnimationPlaybackState.InitialBuffering);
        _session.SetAnimationBuffering(false);
        ScheduleInitialBufferingIndicator();
    }

    private void EnterRemainingBuffering()
    {
        CancelScheduledAdvance();
        CancelBufferingIndicatorDelay();
        SetState(
            ImageAnimationPlaybackState.RemainingBuffering);
        _session.SetAnimationBuffering(true);
        HandleFrameAvailabilityChanged();
    }

    private void StartPlaying()
    {
        CancelBufferingIndicatorDelay();
        _session.SetAnimationBuffering(false);

        if (!IsSelectedFrameAvailable())
        {
            EnterRemainingBuffering();
            return;
        }

        SetState(ImageAnimationPlaybackState.Playing);
        SchedulePlayback();
    }

    private void StopAfterDecodingFailure()
    {
        CancelScheduledAdvance();
        CancelBufferingIndicatorDelay();
        _session.SetAnimationBuffering(false);
        SetState(ImageAnimationPlaybackState.Failed);
    }

    private void SchedulePlayback()
    {
        CancelScheduledAdvance();

        if ((State != ImageAnimationPlaybackState.Playing)
            || !CanPlay())
        {
            return;
        }

        OperationCancellation cancellation = new();
        int expectedFrameIndex =
            _session.SelectedFrameIndex;
        TimeSpan duration =
            _frameSource.CurrentFrameDuration;
        _playbackCancellation = cancellation;
        _playbackTask = AdvanceAfterDelayAsync(
            expectedFrameIndex,
            duration,
            cancellation);
    }

    private async Task AdvanceAfterDelayAsync(
        int expectedFrameIndex,
        TimeSpan duration,
        OperationCancellation cancellation)
    {
        CancellationToken ct = cancellation.Token;

        try
        {
            await _delayScheduler
                .DelayAsync(duration, ct)
                .ConfigureAwait(false);
            await _uiDispatcher.InvokeAsync(
                () =>
                {
                    if (!CanAdvance(
                        expectedFrameIndex,
                        cancellation))
                    {
                        return;
                    }

                    AdvanceFrame(expectedFrameIndex);
                },
                ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
            when (cancellation.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to advance an animated Pica image frame.");
        }
        finally
        {
            if (object.ReferenceEquals(
                _playbackCancellation,
                cancellation))
            {
                _playbackCancellation = null;
                _playbackTask = null;
            }

            cancellation.Complete();
        }
    }

    private bool CanAdvance(
        int expectedFrameIndex,
        OperationCancellation cancellation)
    {
        return !_disposed
            && !cancellation.IsCancellationRequested
            && (State
                == ImageAnimationPlaybackState.Playing)
            && _session.IsAnimationPlaybackEnabled
            && (_session.SelectedFrameIndex
                == expectedFrameIndex)
            && (_session.FrameCount
                == _frameSource.FrameCount);
    }

    private void AdvanceFrame(int expectedFrameIndex)
    {
        bool completesIteration =
            expectedFrameIndex
            == _frameSource.FrameCount - 1;
        uint animationIterations =
            _frameSource.AnimationIterations;

        if (completesIteration
            && (animationIterations > 0)
            && (_completedIterations + 1
                >= animationIterations))
        {
            _completedIterations++;
            SetState(
                ImageAnimationPlaybackState.Completed);
            return;
        }

        int nextFrameIndex =
            (expectedFrameIndex + 1)
            % _frameSource.FrameCount;

        if (!_frameSource.IsFrameAvailable(
            nextFrameIndex))
        {
            EnterRemainingBuffering();
            return;
        }

        if (completesIteration)
        {
            _completedIterations++;
        }

        _session.AdvanceAnimationFrame();
    }

    private bool HasCompletedPlayback()
    {
        uint animationIterations =
            _frameSource.AnimationIterations;

        return (animationIterations > 0)
            && (_completedIterations
                >= animationIterations);
    }

    private void ScheduleInitialBufferingIndicator()
    {
        CancelBufferingIndicatorDelay();
        OperationCancellation cancellation = new();
        _bufferingIndicatorCancellation = cancellation;
        _bufferingIndicatorTask =
            ShowInitialBufferingIndicatorAsync(
                cancellation);
    }

    private async Task ShowInitialBufferingIndicatorAsync(
        OperationCancellation cancellation)
    {
        CancellationToken ct = cancellation.Token;

        try
        {
            await _delayScheduler.DelayAsync(
                InitialBufferingIndicatorDelay,
                ct).ConfigureAwait(false);
            await _uiDispatcher.InvokeAsync(
                () =>
                {
                    if (_disposed
                        || cancellation
                            .IsCancellationRequested
                        || (State
                            != ImageAnimationPlaybackState.InitialBuffering)
                        || _frameSource
                            .IsPlaybackStartBufferReady
                        || _frameSource
                            .IsDecodingComplete)
                    {
                        return;
                    }

                    _session.SetAnimationBuffering(true);
                },
                ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
            when (cancellation.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to show the Pica animation buffering indicator.");
        }
        finally
        {
            if (object.ReferenceEquals(
                _bufferingIndicatorCancellation,
                cancellation))
            {
                _bufferingIndicatorCancellation = null;
                _bufferingIndicatorTask = null;
            }

            cancellation.Complete();
        }
    }

    private void CancelScheduledAdvance()
    {
        OperationCancellation? cancellation =
            _playbackCancellation;
        _playbackCancellation = null;
        _playbackTask = null;
        cancellation?.Cancel();
    }

    private void CancelBufferingIndicatorDelay()
    {
        OperationCancellation? cancellation =
            _bufferingIndicatorCancellation;
        _bufferingIndicatorCancellation = null;
        _bufferingIndicatorTask = null;
        cancellation?.Cancel();
    }

    private void HandleFrameAvailabilityChanged()
    {
        ImageAnimationPlaybackState state = State;

        if (state
            == ImageAnimationPlaybackState.InitialBuffering)
        {
            if (HasDecodingFailed())
            {
                StopAfterDecodingFailure();
                return;
            }

            if (_frameSource
                .IsPlaybackStartBufferReady)
            {
                StartPlaying();
                return;
            }

            return;
        }

        if (state
            != ImageAnimationPlaybackState.RemainingBuffering)
        {
            return;
        }

        if (HasDecodingFailed())
        {
            StopAfterDecodingFailure();
            return;
        }

        if (IsRemainingBufferReady())
        {
            StartPlaying();
            return;
        }
    }

    private bool HasDecodingFailed()
    {
        return _frameSource.IsDecodingComplete
            && !_frameSource.IsFullyDecoded;
    }

    private bool IsRemainingBufferReady()
    {
        int nextFrameIndex =
            (_session.SelectedFrameIndex + 1)
            % _frameSource.FrameCount;

        return IsSelectedFrameAvailable()
            && _frameSource.IsPlaybackStartBufferReady
            && _frameSource.IsFrameAvailable(
                nextFrameIndex);
    }

    private bool IsSelectedFrameAvailable()
    {
        return _frameSource.IsFrameAvailable(
            _session.SelectedFrameIndex);
    }

    private async Task ApplyFrameAvailabilityChangedAsync()
    {
        try
        {
            await _uiDispatcher.InvokeAsync(
                HandleFrameAvailabilityChanged,
                CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to update Pica animation buffering state.");
        }
    }

    private void SetState(
        ImageAnimationPlaybackState state)
    {
        Volatile.Write(
            ref _stateValue,
            (int)state);
    }

    private async Task ObserveDisposalAsync(
        Task disposalTask)
    {
        try
        {
            await disposalTask.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to stop animated Pica image playback.");
        }
    }

    private void OnSessionPropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        _ = sender;

        if (!string.Equals(
            e.PropertyName,
            nameof(ImageViewerSession.SelectedFrameIndex),
            StringComparison.Ordinal))
        {
            return;
        }

        if (State == ImageAnimationPlaybackState.Playing)
        {
            if (!IsSelectedFrameAvailable())
            {
                EnterRemainingBuffering();
                return;
            }

            SchedulePlayback();
            return;
        }

        if ((State
                == ImageAnimationPlaybackState.InitialBuffering)
            && !IsSelectedFrameAvailable())
        {
            EnterRemainingBuffering();
        }
    }

    private void OnFramesChanged(
        object? sender,
        EventArgs e)
    {
        _ = sender;
        _ = e;
        _completedIterations = 0;
        ConfigurePlayback();
    }

    private void OnFrameAvailabilityChanged(
        object? sender,
        EventArgs e)
    {
        _ = sender;
        _ = e;
        ImageAnimationPlaybackState state = State;

        if ((state
                == ImageAnimationPlaybackState.InitialBuffering)
            || (state
                == ImageAnimationPlaybackState.RemainingBuffering))
        {
            _ = ApplyFrameAvailabilityChangedAsync();
        }
    }
}
