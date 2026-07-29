using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

using Pica.Viewer.Services;
using Pica.Viewer.ViewModels;

namespace Pica.Viewer.Views;

internal sealed class ViewerWindowResizeController
{
    private readonly ImageViewerWindow _window;
    private readonly ImageViewerView _view;
    private readonly ImageViewerSettingsViewModel _settings;
    private readonly ImageViewportController _viewport;
    private readonly ViewerWindowModeController _windowMode;
    private readonly ViewerWindowGeometryController _geometry;
    private IWindowResizeSession? _session;

    internal ViewerWindowResizeController(
        ImageViewerWindow window,
        ImageViewerView view,
        ImageViewerSettingsViewModel settings,
        ImageViewportController viewport,
        ViewerWindowModeController windowMode,
        ViewerWindowGeometryController geometry)
    {
        _window = window
            ?? throw new ArgumentNullException(nameof(window));
        _view = view ?? throw new ArgumentNullException(nameof(view));
        _settings = settings
            ?? throw new ArgumentNullException(nameof(settings));
        _viewport = viewport
            ?? throw new ArgumentNullException(nameof(viewport));
        _windowMode = windowMode
            ?? throw new ArgumentNullException(nameof(windowMode));
        _geometry = geometry
            ?? throw new ArgumentNullException(nameof(geometry));
    }

    internal void OnPointerPressed(
        object? sender,
        PointerPressedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        if (!_windowMode.IsWindowed
            || (_viewport.CurrentBitmap is null)
            || (sender
                is not Border
                {
                    Tag: WindowSizingEdges sizingEdges
                }))
        {
            return;
        }

        PointerPoint pointerPoint =
            e.GetCurrentPoint(_window);

        if (!pointerPoint.Properties.IsLeftButtonPressed)
        {
            return;
        }

        double scaling = _window.RenderScaling;
        int width = Math.Max(
            1,
            (int)Math.Round(
                _window.ClientSize.Width * scaling));
        int height = Math.Max(
            1,
            (int)Math.Round(
                _window.ClientSize.Height * scaling));
        WindowRectangle initialRectangle = new()
        {
            Left = _window.Position.X,
            Top = _window.Position.Y,
            Right = _window.Position.X + width,
            Bottom = _window.Position.Y + height
        };
        PixelPoint pointerPosition =
            VisualExtensions.PointToScreen(
                _view.Root,
                e.GetPosition(_view.Root));
        int titleBarHeight = (int)Math.Round(
            _geometry.GetWindowedTitleBarHeight()
                * scaling);
        double aspectRatio =
            (double)_viewport.CurrentBitmap.PixelSize.Width
            / _viewport.CurrentBitmap.PixelSize.Height;
        _session = CreateSession(
            initialRectangle,
            pointerPosition,
            sizingEdges,
            titleBarHeight,
            aspectRatio);
        e.Pointer.Capture((InputElement)sender);
        e.Handled = true;
    }

    internal void OnPointerMoved(PointerEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        if (_session is null)
        {
            return;
        }

        PixelPoint pointerPosition =
            VisualExtensions.PointToScreen(
                _view.Root,
                e.GetPosition(_view.Root));
        WindowRectangle rectangle =
            _session.Calculate(pointerPosition);
        _geometry.ApplyRectangle(rectangle);
        e.Handled = true;
    }

    internal void OnPointerReleased(
        PointerReleasedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        if (_session is null)
        {
            return;
        }

        _session = null;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private IWindowResizeSession CreateSession(
        WindowRectangle initialRectangle,
        PixelPoint pointerPosition,
        WindowSizingEdges sizingEdges,
        int titleBarHeight,
        double aspectRatio)
    {
        if (_settings.ResizeBehavior
            == WindowResizeBehavior.AlwaysFitImage)
        {
            return new AspectRatioWindowResizeSession(
                initialRectangle,
                pointerPosition,
                sizingEdges,
                0,
                titleBarHeight,
                aspectRatio);
        }

        return new FreeWindowResizeSession(
            initialRectangle,
            pointerPosition,
            sizingEdges);
    }
}
