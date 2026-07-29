using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SukiUI.Controls;

namespace Pica.Viewer.Views;

public sealed partial class ImageViewerWindow : SukiWindow
{
    private static Point CalculateToolMenuPosition(
        Point anchorPosition,
        Size anchorSize,
        Size menuSize,
        Size viewport)
    {
        double maxX = Math.Max(0d, viewport.Width - menuSize.Width);
        double maxY = Math.Max(0d, viewport.Height - menuSize.Height);
        double x = anchorPosition.X + anchorSize.Width - menuSize.Width;
        double y = anchorPosition.Y - menuSize.Height - ContextMenuGap;

        return new Point(
            Math.Clamp(x, 0d, maxX),
            Math.Clamp(y, 0d, maxY));
    }

    private ViewerImageMode GetViewerImageMode()
    {
        return _channelSelection.IsActive
            ? ViewerImageMode.Channels
            : ViewerImageMode.Main;
    }

    private void ShowToolMenu()
    {
        HideContextMenu();
        HideModeSubmenu();
        _view.UpdateFilteringMenuState(_settings.IsFilteringEnabled);
        _view.UpdateImageModeMenuState(GetViewerImageMode());
        _view.ToolMenu.IsVisible = true;
        _view.ToolMenu.Measure(new Size(
            double.PositiveInfinity,
            double.PositiveInfinity));
        Size menuSize = GetMeasuredMenuSize(
            _view.ToolMenu,
            new Size(ToolMenuFallbackWidth, ToolMenuFallbackHeight));
        Point? translatedPosition = _view.ToolMenuButton.TranslatePoint(
            new Point(0d, 0d),
            _view.ToolMenuLayer);

        if (translatedPosition is not { } anchorPosition)
        {
            HideToolMenu();
            return;
        }

        Point menuPosition = CalculateToolMenuPosition(
            anchorPosition,
            _view.ToolMenuButton.Bounds.Size,
            menuSize,
            GetViewportSize());
        Canvas.SetLeft(_view.ToolMenu, menuPosition.X);
        Canvas.SetTop(_view.ToolMenu, menuPosition.Y);
        _view.ToolMenu.Opacity =
            ImageViewerVisualMetrics.VisibleControlsOpacity;
    }

    private void HideToolMenu()
    {
        HideModeSubmenu();
        _view.ToolMenu.Opacity =
            ImageViewerVisualMetrics.HiddenControlsOpacity;
        _view.ToolMenu.IsVisible = false;
    }

    private void OnToolMenuClicked(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        if (_view.ToolMenu.IsVisible)
        {
            HideToolMenu();
            return;
        }

        ShowToolMenu();
    }

    private async void OnFilteringMenuClicked(
        object? sender,
        RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        HideToolMenu();
        await ToggleFilteringAsync();
    }

    private void OnModeMenuClicked(object? sender, RoutedEventArgs e)
    {
        _ = e;

        Control anchor = sender as Control ?? _view.ModeMenuButton;
        CancelSubmenuHide();
        ShowModeSubmenu(anchor);
    }

    private void OnMainModeMenuClicked(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        HideToolMenu();

        if (_channelSelection.IsActive)
        {
            ExitChannelMode();
        }
    }

    private void OnChannelModeMenuClicked(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        HideToolMenu();
        EnterChannelMode();
    }

    private void OnModeMenuAnchorPointerEntered(
        object? sender,
        PointerEventArgs e)
    {
        _ = e;

        if (!_view.ToolMenu.IsVisible || (sender is not Control anchor))
        {
            return;
        }

        CancelSubmenuHide();
        ShowModeSubmenu(anchor);
    }
}
