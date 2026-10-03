using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

using Pica.Viewer.Services;
using Pica.Viewer.ViewModels;

namespace Pica.Viewer.Views;

internal sealed class ViewerWindowResizeController : IDisposable
{
    internal bool IsActive => _session is not null;

    private readonly ImageViewerWindow _window;
    private readonly ImageViewerView _view;
    private readonly ImageViewerSettingsViewModel _settings;
    private readonly ImageViewportController _viewport;
    private readonly ViewerWindowModeController _windowMode;
    private readonly ViewerWindowGeometryController _geometry;
    private IWindowResizeSession? _session;
    private Border? _resizeGrip;
    private IPointer? _resizePointer;

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

    public void Dispose()
    {
        EndSession();
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
                } resizeGrip))
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

        EndSession();
        _session = CreateSession(
            initialRectangle,
            pointerPosition,
            sizingEdges,
            titleBarHeight,
            aspectRatio);
        _resizeGrip = resizeGrip;
        _resizePointer = e.Pointer;
        resizeGrip.PointerCaptureLost += OnPointerCaptureLost;
        e.Pointer.Capture(resizeGrip);
        _window.UpdateTitleBarVisibility();
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

        EndSession();
        e.Handled = true;
    }

    private void EndSession()
    {
        if (_session is null)
        {
            return;
        }

        Border? resizeGrip = _resizeGrip;
        IPointer? resizePointer = _resizePointer;
        _session = null;
        _resizeGrip = null;
        _resizePointer = null;

        if (resizeGrip is not null)
        {
            resizeGrip.PointerCaptureLost -= OnPointerCaptureLost;
        }

        if ((resizePointer is not null) && (resizePointer.Captured == resizeGrip))
        {
            resizePointer.Capture(null);
        }

        _window.UpdateTitleBarVisibility();
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

    private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _ = sender;
        _ = e;
        EndSession();
    }
}
