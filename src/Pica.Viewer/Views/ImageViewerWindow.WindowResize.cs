using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using SukiUI.Controls;

using Pica.Viewer.Services;

namespace Pica.Viewer.Views;

public sealed partial class ImageViewerWindow : SukiWindow
{
    private void OnWindowPositionChanged(
        object? sender,
        PixelPointEventArgs e)
    {
        _ = sender;
        _windowMode.OnWindowPositionChanged(e);
    }

    private void OnWindowResizePointerPressed(
        object? sender,
        PointerPressedEventArgs e)
    {
        _windowResize.OnPointerPressed(
            sender,
            e);
    }

    private void OnWindowResizePointerMoved(
        object? sender,
        PointerEventArgs e)
    {
        _ = sender;
        _windowResize.OnPointerMoved(e);
    }

    private void OnWindowResizePointerReleased(
        object? sender,
        PointerReleasedEventArgs e)
    {
        _ = sender;
        _windowResize.OnPointerReleased(e);
    }

    private void OnWindowResized(
        object? sender,
        WindowResizedEventArgs e)
    {
        _ = sender;
        _windowMode.OnWindowResized(e);
    }

    private void OnViewerAreaSizeChanged(
        object? sender,
        SizeChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        _windowMode.OnViewerAreaSizeChanged();
    }

    private void OnLeftNavigationPressed(
        object? sender,
        PointerPressedEventArgs e)
    {
        _ = sender;

        if (_selection.IsActive
            && !Session.IsChannelModeActive)
        {
            return;
        }

        Session.NavigateCommand.Execute(-1);
        e.Handled = true;
    }

    private void OnRightNavigationPressed(
        object? sender,
        PointerPressedEventArgs e)
    {
        _ = sender;

        if (_selection.IsActive
            && !Session.IsChannelModeActive)
        {
            return;
        }

        Session.NavigateCommand.Execute(1);
        e.Handled = true;
    }
}
