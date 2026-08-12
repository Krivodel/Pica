using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

using Pica.Viewer.ViewModels;

namespace Pica.Viewer.Controls;

internal sealed partial class ImageContentNavigationControl : UserControl
{
    internal bool IsTimelineInteractionActive =>
        _isAnimationTimelinePointerActive;

    private readonly Border _animationTimeline;
    private bool _isAnimationTimelinePointerActive;

    public ImageContentNavigationControl()
    {
        InitializeComponent();
        _animationTimeline = this.FindControl<Border>(
            "AnimationTimeline")
            ?? throw new InvalidOperationException(
                "The image content navigation control is missing its animation timeline.");
        _animationTimeline.AddHandler(
            PointerPressedEvent,
            OnAnimationTimelinePointerPressed,
            RoutingStrategies.Tunnel,
            true);
        _animationTimeline.AddHandler(
            PointerMovedEvent,
            OnAnimationTimelinePointerMoved,
            RoutingStrategies.Tunnel,
            true);
        _animationTimeline.AddHandler(
            PointerReleasedEvent,
            OnAnimationTimelinePointerReleased,
            RoutingStrategies.Tunnel,
            true);
        _animationTimeline.PointerCaptureLost +=
            OnAnimationTimelinePointerCaptureLost;
    }

    private void SeekAnimation(Point position)
    {
        if ((DataContext
                is not ImageViewerSessionViewModel viewModel)
            || !viewModel.IsAnimationTimelineEnabled
            || (_animationTimeline.Bounds.Width <= 0d))
        {
            return;
        }

        double ratio = Math.Clamp(
            position.X / _animationTimeline.Bounds.Width,
            0d,
            1d);
        double framePosition =
            ratio * viewModel.AnimationTimelineMaximum;

        if (viewModel.SeekAnimationCommand.CanExecute(
            framePosition))
        {
            viewModel.SeekAnimationCommand.Execute(
                framePosition);
        }
    }

    private void OnAnimationTimelinePointerPressed(
        object? sender,
        PointerPressedEventArgs e)
    {
        _ = sender;
        PointerPoint pointerPoint = e.GetCurrentPoint(
            _animationTimeline);

        if (!pointerPoint.Properties.IsLeftButtonPressed)
        {
            return;
        }

        _isAnimationTimelinePointerActive = true;
        e.Pointer.Capture(_animationTimeline);
        SeekAnimation(pointerPoint.Position);
        e.Handled = true;
    }

    private void OnAnimationTimelinePointerMoved(
        object? sender,
        PointerEventArgs e)
    {
        _ = sender;

        if (!_isAnimationTimelinePointerActive)
        {
            return;
        }

        PointerPoint pointerPoint = e.GetCurrentPoint(
            _animationTimeline);

        if (!pointerPoint.Properties.IsLeftButtonPressed)
        {
            EndTimelineInteraction();
            e.Pointer.Capture(null);
            return;
        }

        SeekAnimation(pointerPoint.Position);
        e.Handled = true;
    }

    private void OnAnimationTimelinePointerReleased(
        object? sender,
        PointerReleasedEventArgs e)
    {
        _ = sender;

        if (!_isAnimationTimelinePointerActive)
        {
            return;
        }

        SeekAnimation(e.GetPosition(_animationTimeline));
        EndTimelineInteraction();
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void OnAnimationTimelinePointerCaptureLost(
        object? sender,
        PointerCaptureLostEventArgs e)
    {
        _ = sender;
        _ = e;
        EndTimelineInteraction();
    }

    private void EndTimelineInteraction()
    {
        _isAnimationTimelinePointerActive = false;
    }
}
