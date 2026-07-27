using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using SukiUI.Controls;

namespace Pica.Viewer.Views;

public sealed partial class ImageViewerWindow : SukiWindow
{
    private void UpdateControlVisibility(Point pointerPosition)
    {
        Size viewport = GetViewportSize();
        Rect viewportRect = new(viewport);
        UpdateImageInformationPanelWidthLimit(viewport);

        if (!viewportRect.Contains(pointerPosition) || IsControlModifierActive())
        {
            HideViewerControls();
            return;
        }

        if (_isSelectionActive || _isSelecting || _isSelectionArmed)
        {
            HideViewerChrome();
            return;
        }

        double edgeWidth = Math.Max(
            ImageViewerVisualMetrics.ArrowAreaMinWidth,
            viewport.Width * EdgeRevealRatio);
        _view.LeftNavigationArea.Width = edgeWidth;
        _view.RightNavigationArea.Width = edgeWidth;
        SetControlVisibility(_view.LeftNavigationArea, pointerPosition.X <= edgeWidth);
        SetControlVisibility(_view.RightNavigationArea, pointerPosition.X >= viewport.Width - edgeWidth);
        SetControlVisibility(_view.BottomControls, pointerPosition.Y >= viewport.Height - BottomRevealSize);
        double informationRevealWidth = Math.Max(
            ImageViewerVisualMetrics.InformationRevealWidth,
            _view.ImageInformationPanel.Bounds.Width
                + ImageViewerVisualMetrics.InformationPanelMargin);
        double informationRevealHeight = Math.Max(
            ImageViewerVisualMetrics.InformationRevealHeight,
            _view.ImageInformationPanel.Bounds.Height
                + ImageViewerVisualMetrics.InformationPanelMargin);
        bool showsImageInformation =
            (pointerPosition.X <= informationRevealWidth)
            && (pointerPosition.Y <= informationRevealHeight);
        SetInformationVisibility(showsImageInformation);
        bool showsWindowButtons = (pointerPosition.X
                >= viewport.Width - ImageViewerVisualMetrics.WindowControlsWidth)
            && (pointerPosition.Y <= ImageViewerVisualMetrics.CloseRevealSize);
        SetControlVisibility(_view.FullscreenSettingsButton, showsWindowButtons);
        SetControlVisibility(_view.WindowModeButton, showsWindowButtons);
        SetControlVisibility(_view.CloseButton, showsWindowButtons);
    }

    private void HideViewerControls()
    {
        HideViewerChrome();
        HideContextMenu();
    }

    private void HideViewerChrome()
    {
        SetControlVisibility(_view.LeftNavigationArea, false);
        SetControlVisibility(_view.RightNavigationArea, false);
        SetControlVisibility(_view.BottomControls, false);
        SetInformationVisibility(false);
        SetControlVisibility(_view.FullscreenSettingsButton, false);
        SetControlVisibility(_view.WindowModeButton, false);
        SetControlVisibility(_view.CloseButton, false);
    }

    private static void SetControlVisibility(InputElement control, bool isVisible)
    {
        control.Opacity = isVisible
            ? ImageViewerVisualMetrics.VisibleControlsOpacity
            : ImageViewerVisualMetrics.HiddenControlsOpacity;
        control.IsHitTestVisible = isVisible;
    }

    private void SetInformationVisibility(bool isVisible)
    {
        _view.ImageInformationPanel.Opacity = isVisible
            ? ImageViewerVisualMetrics.VisibleControlsOpacity
            : ImageViewerVisualMetrics.HiddenControlsOpacity;
    }

    private void UpdateImageInformationPanelWidthLimit(Size viewport)
    {
        _view.ImageInformationPanel.MaxWidth = Math.Max(
            0d,
            viewport.Width
                - ImageViewerVisualMetrics.WindowControlsWidth
                - (ImageViewerVisualMetrics.InformationPanelMargin * 2d));
    }

    private bool IsControlModifierActive()
    {
        return _isControlModifierActive;
    }

    private void ShowContextMenu(Point position)
    {
        if (_isSelectionActive || _isSelecting)
        {
            return;
        }

        Size viewport = GetViewportSize();
        HideOpenWithSubmenu();
        _view.ContextMenu.IsVisible = true;
        _view.ContextMenu.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Size menuSize = GetMeasuredMenuSize(
            _view.ContextMenu,
            new Size(ContextMenuFallbackWidth, ContextMenuFallbackHeight));
        Point menuPosition = CalculateFloatingMenuPosition(position, menuSize, viewport);
        Canvas.SetLeft(_view.ContextMenu, menuPosition.X);
        Canvas.SetTop(_view.ContextMenu, menuPosition.Y);
        _view.ContextMenu.Opacity = ImageViewerVisualMetrics.VisibleControlsOpacity;
    }

    private void HideContextMenu()
    {
        _view.ContextMenu.Opacity = ImageViewerVisualMetrics.HiddenControlsOpacity;
        _view.ContextMenu.IsVisible = false;
        HideOpenWithSubmenu();
    }

    private void ShowOpenWithSubmenu(Control anchor)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        Size viewport = GetViewportSize();
        _view.OpenWithMenu.IsVisible = true;
        _view.OpenWithMenu.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Size submenuSize = GetMeasuredMenuSize(
            _view.OpenWithMenu,
            new Size(OpenWithMenuFallbackWidth, OpenWithMenuFallbackHeight));
        Point? translatedPosition = anchor.TranslatePoint(
            new Point(0d, 0d),
            _view.OpenWithMenuLayer);

        if (translatedPosition is not { } anchorPosition)
        {
            HideOpenWithSubmenu();
            return;
        }

        double menuGap = _openWithTarget == OpenWithTarget.Selection
            ? 0d
            : ContextMenuGap;
        double x = anchorPosition.X + anchor.Bounds.Width + menuGap;

        if ((x + submenuSize.Width) > viewport.Width)
        {
            x = anchorPosition.X - submenuSize.Width - menuGap;
        }

        double maxX = Math.Max(0d, viewport.Width - submenuSize.Width);
        double maxY = Math.Max(0d, viewport.Height - submenuSize.Height);
        Canvas.SetLeft(_view.OpenWithMenu, Math.Clamp(x, 0d, maxX));
        Canvas.SetTop(_view.OpenWithMenu, Math.Clamp(anchorPosition.Y, 0d, maxY));
        _view.OpenWithMenu.Opacity = ImageViewerVisualMetrics.VisibleControlsOpacity;
    }

    private void HideOpenWithSubmenu()
    {
        _openWithMenuHideTimer.Stop();
        _view.OpenWithMenu.Opacity = ImageViewerVisualMetrics.HiddenControlsOpacity;
        _view.OpenWithMenu.IsVisible = false;
        _openWithAnchor = null;
    }

    private void ScheduleOpenWithSubmenuHide()
    {
        _openWithMenuHideTimer.Stop();
        _openWithMenuHideTimer.Start();
    }

    private void CancelOpenWithSubmenuHide()
    {
        _openWithMenuHideTimer.Stop();
    }

    private void OnOpenWithMenuHideTimerTick(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        _openWithMenuHideTimer.Stop();

        bool isAnchorHovered = _openWithAnchor?.IsPointerOver == true;

        if (!isAnchorHovered && !_view.OpenWithMenu.IsPointerOver)
        {
            HideOpenWithSubmenu();
        }
    }

    private void OnContextOpenWithAnchorPointerEntered(object? sender, PointerEventArgs e)
    {
        _ = e;

        if (sender is not Control anchor)
        {
            return;
        }

        CancelOpenWithSubmenuHide();
        ShowOpenWithMenu(OpenWithTarget.CurrentImage, anchor);
    }

    private void OnOpenWithAnchorPointerExited(object? sender, PointerEventArgs e)
    {
        _ = sender;
        _ = e;
        ScheduleOpenWithSubmenuHide();
    }

    private void OnOpenWithMenuPointerEntered(object? sender, PointerEventArgs e)
    {
        _ = sender;
        _ = e;
        CancelOpenWithSubmenuHide();
    }

    private void OnOpenWithMenuPointerExited(object? sender, PointerEventArgs e)
    {
        _ = sender;
        _ = e;
        ScheduleOpenWithSubmenuHide();
    }

    private static Size GetMeasuredMenuSize(Border menu, Size fallbackSize)
    {
        double width = menu.DesiredSize.Width;
        double height = menu.DesiredSize.Height;

        if (double.IsNaN(width) || (width <= 0d) || double.IsInfinity(width))
        {
            width = fallbackSize.Width;
        }

        if (double.IsNaN(height) || (height <= 0d) || double.IsInfinity(height))
        {
            height = fallbackSize.Height;
        }

        return new Size(width, height);
    }

    private static Point CalculateFloatingMenuPosition(Point pointerPosition, Size menuSize, Size viewport)
    {
        double maxX = Math.Max(0d, viewport.Width - menuSize.Width);
        double maxY = Math.Max(0d, viewport.Height - menuSize.Height);
        double x = pointerPosition.X + ContextMenuGap;
        double y = pointerPosition.Y + ContextMenuGap;

        if (x > maxX)
        {
            x = pointerPosition.X - menuSize.Width - ContextMenuGap;
        }

        if (y > maxY)
        {
            y = pointerPosition.Y - menuSize.Height - ContextMenuGap;
        }

        x = Math.Clamp(x, 0d, maxX);
        y = Math.Clamp(y, 0d, maxY);

        return new Point(x, y);
    }
}
