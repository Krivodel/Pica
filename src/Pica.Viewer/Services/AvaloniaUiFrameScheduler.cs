using Avalonia;
using Avalonia.Controls;

namespace Pica.Viewer.Services;

public sealed class AvaloniaUiFrameScheduler :
    IUiFrameScheduler,
    IViewerRenderFrameAwaiter,
    IDisposable
{
    public bool HasPendingFrames => _scheduler.HasPendingFrames;

    private readonly TopLevel _topLevel;
    private readonly AnimationFrameSchedulerCore _scheduler;
    private bool _isObservingPresentation;
    private bool _isDisposed;

    public AvaloniaUiFrameScheduler(TopLevel topLevel)
    {
        _topLevel = topLevel
            ?? throw new ArgumentNullException(nameof(topLevel));
        _scheduler = new AnimationFrameSchedulerCore(SubmitFrame);
    }

    public void RequestAnimationFrame(Action<TimeSpan> frameAction)
    {
        ArgumentNullException.ThrowIfNull(frameAction);
        ThrowIfDisposed();
        StartObservingPresentation();
        UpdatePresentation();
        _scheduler.RequestAnimationFrame(
            frameTime => CompleteFrame(frameAction, frameTime));
    }

    public void CancelPendingFrames()
    {
        ThrowIfDisposed();
        _scheduler.CancelPendingFrames();
        StopObservingPresentation();
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _scheduler.CancelPendingFrames();
        StopObservingPresentation();
    }

    async Task IViewerRenderFrameAwaiter.WaitAsync(CancellationToken ct)
    {
        TaskCompletionSource frameRendered = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        RequestAnimationFrame(_ => frameRendered.TrySetResult());

        await frameRendered.Task
            .WaitAsync(ct)
            .ConfigureAwait(false);
    }

    private bool SubmitFrame(Action<TimeSpan> frameAction)
    {
        _topLevel.RequestAnimationFrame(frameAction);

        return true;
    }

    private void CompleteFrame(
        Action<TimeSpan> frameAction,
        TimeSpan frameTime)
    {
        try
        {
            frameAction(frameTime);
        }
        finally
        {
            if (!_scheduler.HasPendingFrames)
            {
                StopObservingPresentation();
            }
        }
    }

    private void StartObservingPresentation()
    {
        if (_isObservingPresentation)
        {
            return;
        }

        _topLevel.PropertyChanged += OnTopLevelPropertyChanged;

        if (_topLevel is Window window)
        {
            window.Closed += OnWindowClosed;
        }

        _isObservingPresentation = true;
    }

    private void StopObservingPresentation()
    {
        if (!_isObservingPresentation)
        {
            return;
        }

        _topLevel.PropertyChanged -= OnTopLevelPropertyChanged;

        if (_topLevel is Window window)
        {
            window.Closed -= OnWindowClosed;
        }

        _isObservingPresentation = false;
    }

    private void UpdatePresentation()
    {
        bool isPresented = _topLevel.IsVisible
            && ((_topLevel is not Window window)
                || (window.WindowState != WindowState.Minimized));
        _scheduler.SetPresentation(isPresented);
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(
                nameof(AvaloniaUiFrameScheduler));
        }
    }

    private void OnTopLevelPropertyChanged(
        object? sender,
        AvaloniaPropertyChangedEventArgs e)
    {
        _ = sender;

        if ((e.Property == Visual.IsVisibleProperty)
            || (e.Property == Window.WindowStateProperty))
        {
            UpdatePresentation();
        }
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        Dispose();
    }
}
