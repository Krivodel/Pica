using Avalonia.Media;

using Pica.Viewer.Resources;

namespace Pica.Viewer.Views;

internal sealed class ViewerSettingsPanelController
{
    private const double AnimationDurationSeconds = 0.16d;

    private static readonly TimeSpan AnimationDuration =
        TimeSpan.FromSeconds(AnimationDurationSeconds);

    private readonly ImageViewerView _view;
    private readonly ViewerFrameAnimationRunner _animationRunner;
    private long _animationId;

    internal ViewerSettingsPanelController(
        ImageViewerView view,
        ViewerFrameAnimationRunner animationRunner)
    {
        _view = view ?? throw new ArgumentNullException(nameof(view));
        _animationRunner = animationRunner
            ?? throw new ArgumentNullException(nameof(animationRunner));
    }

    internal void Toggle()
    {
        if (_view.SettingsPanel is { IsVisible: true, IsHitTestVisible: true })
        {
            Hide();
            return;
        }

        Show();
    }

    internal void Show()
    {
        _view.SettingsPanel.IsVisible = true;
        _view.SettingsPanel.IsHitTestVisible = true;
        StartAnimation(
            _view.VisibleControlsOpacity,
            0d,
            null);
    }

    internal void Hide()
    {
        _view.SettingsPanel.IsHitTestVisible = false;
        StartAnimation(
            _view.HiddenControlsOpacity,
            ImageViewerVisualMetrics.SettingsPanelHiddenOffset,
            CompleteHide);
    }

    internal void HideImmediately()
    {
        _animationId++;
        _view.SettingsPanel.IsHitTestVisible = false;
        _view.SettingsPanel.IsVisible = false;
        _view.SettingsPanel.Opacity = _view.HiddenControlsOpacity;

        if (_view.SettingsPanel.RenderTransform
            is TranslateTransform transform)
        {
            transform.Y =
                ImageViewerVisualMetrics.SettingsPanelHiddenOffset;
        }
    }

    private void StartAnimation(
        double targetOpacity,
        double targetOffset,
        Action? completed)
    {
        if (_view.SettingsPanel.RenderTransform
            is not TranslateTransform transform)
        {
            return;
        }

        long animationId = ++_animationId;
        double startOpacity = _view.SettingsPanel.Opacity;
        double startOffset = transform.Y;

        _animationRunner.Start(
            AnimationDuration,
            () => animationId == _animationId,
            progress =>
            {
                double easedProgress =
                    ViewerFrameAnimationRunner.EaseOutCubic(progress);
                _view.SettingsPanel.Opacity = startOpacity
                    + ((targetOpacity - startOpacity) * easedProgress);
                transform.Y = startOffset
                    + ((targetOffset - startOffset) * easedProgress);
            },
            completed: completed);
    }

    private void CompleteHide()
    {
        _view.SettingsPanel.IsVisible = false;
    }
}
