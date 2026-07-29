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
        HideToolMenu();
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

        HideToolMenu();
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
        double menuGap = _openWithTarget == OpenWithTarget.Selection
            ? 0d
            : ContextMenuGap;
        ShowSubmenu(
            _view.OpenWithMenu,
            _view.OpenWithMenuLayer,
            anchor,
            new Size(OpenWithMenuFallbackWidth, OpenWithMenuFallbackHeight),
            menuGap);
    }

    private void ShowModeSubmenu(Control anchor)
    {
        ShowSubmenu(
            _view.ModeMenu,
            _view.ToolMenuLayer,
            anchor,
            new Size(ModeMenuFallbackWidth, ModeMenuFallbackHeight),
            ContextMenuGap);
    }

    private void ShowSubmenu(
        Border submenu,
        Canvas submenuLayer,
        Control anchor,
        Size fallbackSize,
        double menuGap)
    {
        ArgumentNullException.ThrowIfNull(submenu);
        ArgumentNullException.ThrowIfNull(submenuLayer);
        ArgumentNullException.ThrowIfNull(anchor);

        HideActiveSubmenu();
        _activeSubmenu = submenu;
        _submenuAnchor = anchor;

        Size viewport = GetViewportSize();
        submenu.IsVisible = true;
        submenu.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Size submenuSize = GetMeasuredMenuSize(submenu, fallbackSize);
        Point? translatedPosition = anchor.TranslatePoint(
            new Point(0d, 0d),
            submenuLayer);

        if (translatedPosition is not { } anchorPosition)
        {
            HideActiveSubmenu();
            return;
        }

        double x = anchorPosition.X + anchor.Bounds.Width + menuGap;

        if ((x + submenuSize.Width) > viewport.Width)
        {
            x = anchorPosition.X - submenuSize.Width - menuGap;
        }

        double maxX = Math.Max(0d, viewport.Width - submenuSize.Width);
        double maxY = Math.Max(0d, viewport.Height - submenuSize.Height);
        Canvas.SetLeft(submenu, Math.Clamp(x, 0d, maxX));
        Canvas.SetTop(submenu, Math.Clamp(anchorPosition.Y, 0d, maxY));
        submenu.Opacity = ImageViewerVisualMetrics.VisibleControlsOpacity;
    }

    private void HideOpenWithSubmenu()
    {
        HideSubmenu(_view.OpenWithMenu);
    }

    private void HideModeSubmenu()
    {
        HideSubmenu(_view.ModeMenu);
    }

    private void HideActiveSubmenu()
    {
        Border? submenu = _activeSubmenu;

        if (submenu is not null)
        {
            HideSubmenu(submenu);
        }
    }

    private void HideSubmenu(Border submenu)
    {
        ArgumentNullException.ThrowIfNull(submenu);

        submenu.Opacity = ImageViewerVisualMetrics.HiddenControlsOpacity;
        submenu.IsVisible = false;

        if (ReferenceEquals(_activeSubmenu, submenu))
        {
            _submenuHideTimer.Stop();
            _activeSubmenu = null;
            _submenuAnchor = null;
        }
    }

    private void ScheduleSubmenuHide()
    {
        _submenuHideTimer.Stop();
        _submenuHideTimer.Start();
    }

    private void CancelSubmenuHide()
    {
        _submenuHideTimer.Stop();
    }

    private void OnSubmenuHideTimerTick(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        _submenuHideTimer.Stop();

        bool isAnchorHovered = _submenuAnchor?.IsPointerOver == true;
        bool isSubmenuHovered = _activeSubmenu?.IsPointerOver == true;

        if (!isAnchorHovered && !isSubmenuHovered)
        {
            HideActiveSubmenu();
        }
    }

    private void OnContextOpenWithAnchorPointerEntered(object? sender, PointerEventArgs e)
    {
        _ = e;

        if (sender is not Control anchor)
        {
            return;
        }

        CancelSubmenuHide();
        ShowOpenWithMenu(OpenWithTarget.CurrentImage, anchor);
    }

    private void OnSubmenuAnchorPointerExited(object? sender, PointerEventArgs e)
    {
        _ = sender;
        _ = e;

        ScheduleSubmenuHide();
    }

    private void OnSubmenuPointerEntered(object? sender, PointerEventArgs e)
    {
        _ = sender;
        _ = e;

        CancelSubmenuHide();
    }

    private void OnSubmenuPointerExited(object? sender, PointerEventArgs e)
    {
        _ = sender;
        _ = e;

        ScheduleSubmenuHide();
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
