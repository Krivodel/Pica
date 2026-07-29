using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;

using Pica.Viewer.Services;
using Pica.Viewer.ViewModels;

namespace Pica.Viewer.Views;

internal sealed class ViewerWindowPlacementController
{
    internal Size? WindowedClientSize =>
        _windowedClientSize;

    private const double DefaultWindowWidth = 1280d;
    private const double DefaultWindowHeight = 800d;

    private readonly ImageViewerWindow _window;
    private readonly ViewerWindowPlacementProvider
        _windowPlacementProvider;
    private readonly ImageViewportController _viewport;
    private readonly ViewerWindowGeometryController _geometry;
    private PixelPoint? _windowedPosition;
    private Size? _windowedClientSize;
    private double _windowedPreferredExtent;

    internal ViewerWindowPlacementController(
        ImageViewerWindow window,
        ImageViewerSettingsViewModel settings,
        ViewerWindowPlacementProvider windowPlacementProvider,
        ImageViewportController viewport,
        ViewerWindowGeometryController geometry)
    {
        _window = window
            ?? throw new ArgumentNullException(nameof(window));
        ArgumentNullException.ThrowIfNull(settings);
        _windowPlacementProvider = windowPlacementProvider
            ?? throw new ArgumentNullException(
                nameof(windowPlacementProvider));
        _viewport = viewport
            ?? throw new ArgumentNullException(nameof(viewport));
        _geometry = geometry
            ?? throw new ArgumentNullException(nameof(geometry));
        _windowedPosition = settings.RememberWindowPlacement
            ? CreateWindowedPosition(settings)
            : null;
        _windowedClientSize = settings.RememberWindowPlacement
            ? CreateWindowedClientSize(settings)
            : null;
        Size initialWindowedClientSize = _windowedClientSize
            ?? new Size(
                DefaultWindowWidth,
                DefaultWindowHeight);
        _windowedPreferredExtent = Math.Max(
            initialWindowedClientSize.Width,
            Math.Max(
                1d,
                initialWindowedClientSize.Height
                    - _geometry.GetWindowedTitleBarHeight()));
    }

    internal void ApplyConfiguredGeometry()
    {
        if (_windowedClientSize is not { } windowedClientSize)
        {
            return;
        }

        ApplyWindowedClientSize(windowedClientSize);
        ApplyWindowedPosition(windowedClientSize);
    }

    internal void FitWindowToCurrentImage()
    {
        if (_viewport.CurrentBitmap is null)
        {
            return;
        }

        Size imageSize = ImageWindowGeometry.FitImage(
            _viewport.GetCurrentSourcePixelSize(),
            _windowedPreferredExtent,
            GetMaximumWindowedImageSize());
        Size targetSize = new(
            imageSize.Width,
            imageSize.Height
                + _geometry.GetWindowedTitleBarHeight());
        ApplyWindowedClientSize(targetSize);
        ApplyWindowedPosition(targetSize);
    }

    internal void RestoreWindowedGeometry()
    {
        Size maximumImageSize =
            GetMaximumWindowedImageSize();
        Size maximumWindowSize = new(
            maximumImageSize.Width,
            maximumImageSize.Height
                + _geometry.GetWindowedTitleBarHeight());
        Size storedSize = _windowedClientSize
            ?? new Size(
                DefaultWindowWidth,
                DefaultWindowHeight);
        Size targetSize = new(
            Math.Min(
                storedSize.Width,
                maximumWindowSize.Width),
            Math.Min(
                storedSize.Height,
                maximumWindowSize.Height));
        ApplyWindowedClientSize(targetSize);
        ApplyWindowedPosition(targetSize);
    }

    internal void Reset()
    {
        _windowedPosition = null;
        _windowedClientSize = null;
        _windowedPreferredExtent = Math.Max(
            DefaultWindowWidth,
            DefaultWindowHeight);
    }

    internal void Capture(ViewerWindowMode mode)
    {
        if ((mode != ViewerWindowMode.Windowed)
            || (_window.WindowState != WindowState.Normal))
        {
            return;
        }

        if (_window.ClientSize
            is { Width: > 0d, Height: > 0d })
        {
            _windowedClientSize = _window.ClientSize;
            _windowedPreferredExtent = Math.Max(
                _window.ClientSize.Width,
                Math.Max(
                    1d,
                    _window.ClientSize.Height
                        - _geometry.GetWindowedTitleBarHeight()));
        }

        _windowedPosition = _window.Position;
    }

    internal void Update(ViewerWindowMode mode)
    {
        _windowPlacementProvider.Update(
            new ViewerWindowPlacement(
                mode == ViewerWindowMode.Windowed,
                _windowedPosition?.X,
                _windowedPosition?.Y,
                _windowedClientSize?.Width,
                _windowedClientSize?.Height));
    }

    internal void OnWindowPositionChanged(
        PixelPointEventArgs e,
        ViewerWindowMode mode)
    {
        ArgumentNullException.ThrowIfNull(e);

        if ((mode == ViewerWindowMode.Windowed)
            && !_geometry.IsApplying
            && (_window.WindowState == WindowState.Normal))
        {
            _windowedPosition = e.Point;
            Update(mode);
        }
    }

    internal void OnWindowResized(
        WindowResizedEventArgs e,
        ViewerWindowMode mode)
    {
        ArgumentNullException.ThrowIfNull(e);

        if ((e.ClientSize.Width <= 0d)
            || (e.ClientSize.Height <= 0d))
        {
            return;
        }

        if ((mode == ViewerWindowMode.Windowed)
            && !_geometry.IsApplying
            && (_window.WindowState == WindowState.Normal))
        {
            _windowedClientSize = e.ClientSize;
            _windowedPreferredExtent = Math.Max(
                e.ClientSize.Width,
                Math.Max(
                    1d,
                    e.ClientSize.Height
                        - _geometry.GetWindowedTitleBarHeight()));
            Update(mode);
        }
    }

    private static PixelPoint? CreateWindowedPosition(
        ImageViewerSettingsViewModel settings)
    {
        return settings
            is { WindowX: { } x, WindowY: { } y }
            ? new PixelPoint(x, y)
            : null;
    }

    private static Size? CreateWindowedClientSize(
        ImageViewerSettingsViewModel settings)
    {
        return settings
            is
        {
            WindowWidth: { } width,
            WindowHeight: { } height
        }
            ? new Size(width, height)
            : null;
    }

    private void ApplyWindowedClientSize(Size clientSize)
    {
        _geometry.Apply(() =>
        {
            _window.Width = clientSize.Width;
            _window.Height = clientSize.Height;
            _windowedClientSize = clientSize;
        });
    }

    private void ApplyWindowedPosition(Size clientSize)
    {
        Screen? screen = GetWindowedScreen();

        if (screen is null)
        {
            return;
        }

        double scaling = screen.Scaling;
        int windowPixelWidth =
            (int)Math.Ceiling(clientSize.Width * scaling);
        int windowPixelHeight =
            (int)Math.Ceiling(clientSize.Height * scaling);
        int maximumLeft = Math.Max(
            screen.WorkingArea.X,
            screen.WorkingArea.Right - windowPixelWidth);
        int maximumTop = Math.Max(
            screen.WorkingArea.Y,
            screen.WorkingArea.Bottom - windowPixelHeight);
        int left = _windowedPosition?.X
            ?? screen.WorkingArea.X
                + ((screen.WorkingArea.Width
                    - windowPixelWidth)
                    / 2);
        int top = _windowedPosition?.Y
            ?? screen.WorkingArea.Y
                + ((screen.WorkingArea.Height
                    - windowPixelHeight)
                    / 2);
        PixelPoint position = new(
            Math.Clamp(
                left,
                screen.WorkingArea.X,
                maximumLeft),
            Math.Clamp(
                top,
                screen.WorkingArea.Y,
                maximumTop));

        _geometry.Apply(() =>
        {
            _window.WindowStartupLocation =
                WindowStartupLocation.Manual;
            _window.Position = position;
            _windowedPosition = position;
        });
    }

    private Size GetMaximumWindowedImageSize()
    {
        Screen? screen = GetWindowedScreen();

        if (screen is null)
        {
            return new Size(
                DefaultWindowWidth,
                DefaultWindowHeight);
        }

        double maximumWidth =
            screen.WorkingArea.Width / screen.Scaling;
        double maximumHeight = Math.Max(
            1d,
            (screen.WorkingArea.Height / screen.Scaling)
                - _geometry.GetWindowedTitleBarHeight());

        return new Size(maximumWidth, maximumHeight);
    }

    private Screen? GetWindowedScreen()
    {
        Screen? storedScreen =
            _windowedPosition is { } storedPosition
            ? _window.Screens.ScreenFromPoint(storedPosition)
            : null;

        return storedScreen
            ?? _window.Screens.ScreenFromWindow(_window)
            ?? _window.Screens.Primary;
    }
}
