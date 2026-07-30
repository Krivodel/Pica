using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

using Pica.Viewer.Services;
using Pica.Viewer.ViewModels;

namespace Pica.Viewer.Views;

internal sealed class ViewerWindowModeController : IDisposable
{
    internal ViewerWindowMode Mode => _isWindowedMode
        ? ViewerWindowMode.Windowed
        : ViewerWindowMode.FullScreen;
    internal bool IsWindowed => _isWindowedMode;
    internal Size? WindowedClientSize =>
        _placement.WindowedClientSize;

    private const int LayoutSettleDelayMilliseconds = 100;

    private readonly ImageViewerWindow _window;
    private readonly ImageViewerView _view;
    private readonly ImageViewerSettingsViewModel _settings;
    private readonly ViewerWindowPlacementController _placement;
    private readonly ImageViewportController _viewport;
    private readonly IUiFrameScheduler
        _animationFrameScheduler;
    private readonly Action _hideSettingsPanelImmediately;
    private readonly DispatcherTimer _layoutTimer;
    private bool _isChangingWindowMode;
    private bool _isWindowResizeLayoutPending;
    private bool _isWindowModeLayoutSettling;
    private bool _isWindowedMode;

    internal ViewerWindowModeController(
        ImageViewerWindow window,
        ImageViewerView view,
        ImageViewerSettingsViewModel settings,
        ViewerWindowPlacementController placement,
        ImageViewportController viewport,
        IUiFrameScheduler animationFrameScheduler,
        Action hideSettingsPanelImmediately)
    {
        _window = window
            ?? throw new ArgumentNullException(nameof(window));
        _view = view ?? throw new ArgumentNullException(nameof(view));
        _settings = settings
            ?? throw new ArgumentNullException(nameof(settings));
        _placement = placement
            ?? throw new ArgumentNullException(nameof(placement));
        _viewport = viewport
            ?? throw new ArgumentNullException(nameof(viewport));
        _animationFrameScheduler = animationFrameScheduler
            ?? throw new ArgumentNullException(
                nameof(animationFrameScheduler));
        _hideSettingsPanelImmediately =
            hideSettingsPanelImmediately
            ?? throw new ArgumentNullException(
                nameof(hideSettingsPanelImmediately));
        _isWindowedMode = settings.RememberWindowPlacement
            && settings.IsWindowed;
        _layoutTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(
                LayoutSettleDelayMilliseconds)
        };
        _layoutTimer.Tick += OnLayoutTimerTick;
    }

    public void Dispose()
    {
        _layoutTimer.Stop();
        _layoutTimer.Tick -= OnLayoutTimerTick;
    }

    internal void ApplyConfiguredGeometry()
    {
        if (_isWindowedMode
            && (_placement.WindowedClientSize is not null))
        {
            _placement.ApplyConfiguredGeometry();
        }
    }

    internal void ApplyInitialMode()
    {
        _view.UpdateSettingsPanelPlacement(Mode);
        ApplyWindowChrome(Mode);

        if (_isWindowedMode)
        {
            if (UsesFreeWindowResize())
            {
                RestoreWindowedGeometry();
            }
            else
            {
                FitWindowToCurrentImage();
            }
        }

        BeginLayoutSettlement();
    }

    internal void Toggle()
    {
        if (_isWindowedMode)
        {
            EnterFullScreenMode();
            return;
        }

        EnterWindowedMode();
    }

    internal void HandleWindowStateChanged()
    {
        if (_isChangingWindowMode)
        {
            return;
        }

        if ((_window.WindowState == WindowState.FullScreen)
            || (_window.WindowState == WindowState.Maximized))
        {
            EnterFullScreenMode();
        }
        else if ((_window.WindowState == WindowState.Normal)
            && !_isWindowedMode)
        {
            EnterWindowedMode();
        }
    }

    internal void UpdateInformationPanelVisibility()
    {
        _view.ImageInformationPanel.IsVisible =
            !_isWindowedMode
            && !string.IsNullOrWhiteSpace(
                _view.ImageInformationText.Text);
    }

    internal void FitWindowToCurrentImage()
    {
        if (_viewport.CurrentBitmap is null)
        {
            return;
        }

        _placement.FitWindowToCurrentImage();
        ResetScaleAndCenterAfterLayout();
    }

    internal bool ShouldFitWindowToCurrentImage()
    {
        return _isWindowedMode && !UsesFreeWindowResize();
    }

    internal void ResetRememberedPlacement()
    {
        _placement.Reset();
    }

    internal void CapturePlacement()
    {
        _placement.Capture(Mode);
    }

    internal void UpdatePlacement()
    {
        _placement.Update(Mode);
    }

    internal void OnWindowPositionChanged(
        PixelPointEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        _placement.OnWindowPositionChanged(e, Mode);
    }

    internal void OnWindowResized(WindowResizedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        _placement.OnWindowResized(e, Mode);
        ScheduleResizeLayout();
    }

    internal void OnViewerAreaSizeChanged()
    {
        ScheduleResizeLayout();

        if (_isWindowModeLayoutSettling)
        {
            CompleteLayoutSettlement();
        }
    }

    private void EnterWindowedMode()
    {
        _isChangingWindowMode = true;
        _isWindowedMode = true;
        _view.UpdateSettingsPanelPlacement(Mode);

        try
        {
            _window.WindowState = WindowState.Normal;
            ApplyWindowChrome(ViewerWindowMode.Windowed);

            if (UsesFreeWindowResize())
            {
                RestoreWindowedGeometry();
            }
        }
        finally
        {
            _isChangingWindowMode = false;
        }

        UpdatePlacement();
        BeginLayoutSettlement();
    }

    private void EnterFullScreenMode()
    {
        CapturePlacement();
        _isChangingWindowMode = true;
        _isWindowedMode = false;
        _view.UpdateSettingsPanelPlacement(Mode);

        try
        {
            _hideSettingsPanelImmediately();
            ApplyWindowChrome(ViewerWindowMode.FullScreen);
            _window.WindowState = WindowState.FullScreen;
        }
        finally
        {
            _isChangingWindowMode = false;
        }

        UpdatePlacement();
        BeginLayoutSettlement();
    }

    private void ApplyWindowChrome(ViewerWindowMode mode)
    {
        bool isWindowed =
            mode == ViewerWindowMode.Windowed;
        _window.IsTitleBarVisible = isWindowed;
        _window.ShowTitlebarBackground = isWindowed;
        _view.WindowResizeOverlay.IsVisible = isWindowed;
        UpdateInformationPanelVisibility();
        _view.FullscreenSettingsButton.IsVisible = !isWindowed;
        _view.WindowModeButton.IsVisible = !isWindowed;
        _view.CloseButton.IsVisible = !isWindowed;
    }

    private bool UsesFreeWindowResize()
    {
        return _settings.ResizeBehavior
            == WindowResizeBehavior.Free;
    }

    private void RestoreWindowedGeometry()
    {
        _placement.RestoreWindowedGeometry();
        ResetScaleAndCenterAfterLayout();
    }

    private void ResetScaleAndCenterAfterLayout()
    {
        _window.Dispatcher.Post(
            ScheduleResizeLayout,
            DispatcherPriority.Render);
    }

    private void BeginLayoutSettlement()
    {
        _isWindowModeLayoutSettling = true;
        _layoutTimer.Stop();
        _layoutTimer.Start();
        ScheduleResizeLayout();
        _window.Dispatcher.Post(
            CompleteLayoutSettlement,
            DispatcherPriority.Render);
    }

    private void CompleteLayoutSettlement()
    {
        if (!_isWindowModeLayoutSettling)
        {
            return;
        }

        _layoutTimer.Stop();
        _isWindowModeLayoutSettling = false;

        if (ShouldFitWindowToCurrentImage())
        {
            FitWindowToCurrentImage();
            return;
        }

        ResetScaleAndCenterAfterLayout();
    }

    private void ScheduleResizeLayout()
    {
        if (_isWindowResizeLayoutPending)
        {
            return;
        }

        _isWindowResizeLayoutPending = true;
        _animationFrameScheduler.RequestAnimationFrame(
            OnWindowResizeAnimationFrame);
    }

    private void OnWindowResizeAnimationFrame(
        TimeSpan frameTime)
    {
        _ = frameTime;
        _isWindowResizeLayoutPending = false;
        _viewport.ResetScaleAndCenter();
    }

    private void OnLayoutTimerTick(
        object? sender,
        EventArgs e)
    {
        _ = sender;
        _ = e;
        CompleteLayoutSettlement();
    }
}
