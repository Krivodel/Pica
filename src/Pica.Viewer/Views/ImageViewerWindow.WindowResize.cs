using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using SukiUI.Controls;
using Pica.Viewer.Services;

namespace Pica.Viewer.Views;

public sealed partial class ImageViewerWindow : SukiWindow
{
    private void OnWindowPositionChanged(object? sender, PixelPointEventArgs e)
    {
        _ = sender;

        if (_isWindowedMode && !_isApplyingWindowGeometry && (WindowState == WindowState.Normal))
        {
            _windowedPosition = e.Point;
        }
    }

    private void OnWindowResizePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!_isWindowedMode
            || (_bitmap is null)
            || (sender is not Border { Tag: WindowSizingEdges sizingEdges }))
        {
            return;
        }

        PointerPoint pointerPoint = e.GetCurrentPoint(this);

        if (!pointerPoint.Properties.IsLeftButtonPressed)
        {
            return;
        }

        double scaling = RenderScaling;
        int width = Math.Max(1, (int)Math.Round(ClientSize.Width * scaling));
        int height = Math.Max(1, (int)Math.Round(ClientSize.Height * scaling));
        WindowRectangle initialRectangle = new()
        {
            Left = Position.X,
            Top = Position.Y,
            Right = Position.X + width,
            Bottom = Position.Y + height
        };
        PixelPoint pointerPosition = VisualExtensions.PointToScreen(_view.Root, e.GetPosition(_view.Root));
        int titleBarHeight = (int)Math.Round(GetWindowedTitleBarHeight() * scaling);
        double aspectRatio = (double)_bitmap.PixelSize.Width / _bitmap.PixelSize.Height;
        _windowResizeSession = _settings.ResizeBehavior == WindowResizeBehavior.AlwaysFitImage
            ? new AspectRatioWindowResizeSession(
                initialRectangle,
                pointerPosition,
                sizingEdges,
                0,
                titleBarHeight,
                aspectRatio)
            : new FreeWindowResizeSession(
                initialRectangle,
                pointerPosition,
                sizingEdges);
        e.Pointer.Capture((InputElement)sender);
        e.Handled = true;
    }

    private void OnWindowResizePointerMoved(object? sender, PointerEventArgs e)
    {
        _ = sender;

        if (_windowResizeSession is null)
        {
            return;
        }

        PixelPoint pointerPosition = VisualExtensions.PointToScreen(_view.Root, e.GetPosition(_view.Root));
        WindowRectangle rectangle = _windowResizeSession.Calculate(pointerPosition);
        ApplyWindowRectangle(rectangle);
        e.Handled = true;
    }

    private void OnWindowResizePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _ = sender;

        if (_windowResizeSession is null)
        {
            return;
        }

        _windowResizeSession = null;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void ApplyWindowRectangle(WindowRectangle rectangle)
    {
        double scaling = RenderScaling;

        ApplyWindowGeometry(() =>
        {
            Width = rectangle.Width / scaling;
            Height = rectangle.Height / scaling;
            Position = new PixelPoint(rectangle.Left, rectangle.Top);
        });
    }

    private void OnWindowResized(object? sender, WindowResizedEventArgs e)
    {
        _ = sender;

        if ((e.ClientSize.Width <= 0d)
            || (e.ClientSize.Height <= 0d))
        {
            return;
        }

        if (_isWindowedMode
            && !_isApplyingWindowGeometry
            && (WindowState == WindowState.Normal))
        {
            _windowedClientSize = e.ClientSize;
            _windowedPreferredExtent = Math.Max(
                e.ClientSize.Width,
                Math.Max(1d, e.ClientSize.Height - GetWindowedTitleBarHeight()));
        }

        ScheduleWindowResizeLayout();
    }

    private void OnViewerAreaSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        _ = sender;
        _ = e;

        ScheduleWindowResizeLayout();

        if (_isWindowModeLayoutSettling)
        {
            CompleteWindowModeLayoutSettlement();
        }
    }

    private void ScheduleWindowResizeLayout()
    {
        if (_isWindowResizeLayoutPending)
        {
            return;
        }

        _isWindowResizeLayoutPending = true;
        RequestViewerAnimationFrame(OnWindowResizeAnimationFrame);
    }

    private void OnLeftNavigationPressed(object? sender, PointerPressedEventArgs e)
    {
        _ = sender;

        if (_isSelectionActive)
        {
            return;
        }

        Navigate(-1);
        e.Handled = true;
    }

    private void OnRightNavigationPressed(object? sender, PointerPressedEventArgs e)
    {
        _ = sender;

        if (_isSelectionActive)
        {
            return;
        }

        Navigate(1);
        e.Handled = true;
    }
}
