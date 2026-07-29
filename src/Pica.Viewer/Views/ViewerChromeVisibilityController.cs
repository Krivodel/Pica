using Avalonia;
using Avalonia.Input;

using Pica.Viewer.Resources;

namespace Pica.Viewer.Views;

internal sealed class ViewerChromeVisibilityController
{
    internal bool IsControlModifierActive =>
        _isControlModifierActive;

    private const double EdgeRevealRatio = 0.04d;
    private const double BottomRevealSize = 128d;

    private readonly ImageViewerView _view;
    private readonly ImageViewportController _viewport;
    private readonly ImageSelectionController _selection;
    private readonly ViewerFloatingMenuController _floatingMenus;
    private bool _isControlModifierActive;

    internal ViewerChromeVisibilityController(
        ImageViewerView view,
        ImageViewportController viewport,
        ImageSelectionController selection,
        ViewerFloatingMenuController floatingMenus)
    {
        _view = view ?? throw new ArgumentNullException(nameof(view));
        _viewport = viewport
            ?? throw new ArgumentNullException(nameof(viewport));
        _selection = selection
            ?? throw new ArgumentNullException(nameof(selection));
        _floatingMenus = floatingMenus
            ?? throw new ArgumentNullException(nameof(floatingMenus));
    }

    internal void SetControlModifierActive(bool isActive)
    {
        _isControlModifierActive = isActive;
    }

    internal void Update(Point pointerPosition)
    {
        Size viewport = _viewport.GetViewportSize();
        Rect viewportRect = new(viewport);
        UpdateImageInformationPanelWidthLimit(viewport);

        if (!viewportRect.Contains(pointerPosition)
            || _isControlModifierActive)
        {
            HideControls();
            return;
        }

        if (_selection.IsActive
            || _selection.IsSelecting
            || _selection.IsArmed)
        {
            HideChrome();
            return;
        }

        double edgeWidth = Math.Max(
            _view.NavigationAreaMinimumWidth,
            viewport.Width * EdgeRevealRatio);
        _view.LeftNavigationArea.Width = edgeWidth;
        _view.RightNavigationArea.Width = edgeWidth;
        SetControlVisibility(
            _view.LeftNavigationArea,
            pointerPosition.X <= edgeWidth);
        SetControlVisibility(
            _view.RightNavigationArea,
            pointerPosition.X >= viewport.Width - edgeWidth);
        SetControlVisibility(
            _view.BottomControls,
            pointerPosition.Y
                >= viewport.Height - BottomRevealSize);
        UpdateInformationVisibility(pointerPosition);
        UpdateWindowButtonVisibility(pointerPosition, viewport);
    }

    internal void HideControls()
    {
        HideChrome();
        _floatingMenus.HideAll();
    }

    private void HideChrome()
    {
        SetControlVisibility(_view.LeftNavigationArea, false);
        SetControlVisibility(_view.RightNavigationArea, false);
        SetControlVisibility(_view.BottomControls, false);
        SetInformationVisibility(false);
        SetControlVisibility(
            _view.FullscreenSettingsButton,
            false);
        SetControlVisibility(_view.WindowModeButton, false);
        SetControlVisibility(_view.CloseButton, false);
    }

    private void UpdateInformationVisibility(
        Point pointerPosition)
    {
        double informationRevealWidth = Math.Max(
            ImageViewerVisualMetrics.InformationRevealWidth,
            _view.ImageInformationPanel.Bounds.Width
                + _view.InformationPanelMargin);
        double informationRevealHeight = Math.Max(
            ImageViewerVisualMetrics.InformationRevealHeight,
            _view.ImageInformationPanel.Bounds.Height
                + _view.InformationPanelMargin);
        bool isVisible =
            (pointerPosition.X <= informationRevealWidth)
            && (pointerPosition.Y <= informationRevealHeight);
        SetInformationVisibility(isVisible);
    }

    private void UpdateWindowButtonVisibility(
        Point pointerPosition,
        Size viewport)
    {
        bool isVisible = (pointerPosition.X
                >= viewport.Width - _view.WindowControlsWidth)
            && (pointerPosition.Y <= _view.WindowButtonSize);
        SetControlVisibility(
            _view.FullscreenSettingsButton,
            isVisible);
        SetControlVisibility(_view.WindowModeButton, isVisible);
        SetControlVisibility(_view.CloseButton, isVisible);
    }

    private void SetControlVisibility(
        InputElement control,
        bool isVisible)
    {
        control.Opacity = isVisible
            ? _view.VisibleControlsOpacity
            : _view.HiddenControlsOpacity;
        control.IsHitTestVisible = isVisible;
    }

    private void SetInformationVisibility(bool isVisible)
    {
        _view.ImageInformationPanel.Opacity = isVisible
            ? _view.VisibleControlsOpacity
            : _view.HiddenControlsOpacity;
    }

    private void UpdateImageInformationPanelWidthLimit(
        Size viewport)
    {
        _view.ImageInformationPanel.MaxWidth = Math.Max(
            0d,
            viewport.Width
                - _view.WindowControlsWidth
                - (_view.InformationPanelMargin * 2d));
    }
}
