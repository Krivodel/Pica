using Avalonia;

using Pica.Viewer.Services;

namespace Pica.Viewer.Views;

internal sealed class ViewerSelectionInteractionController
{
    private readonly ImageSelectionController _selection;
    private readonly ViewerCursorController _cursor;
    private readonly ViewerChromeVisibilityController _chromeVisibility;
    private readonly ViewerFloatingMenuController _floatingMenus;
    private Point _lastPointerPosition;
    private bool _isPointerPressed;

    internal ViewerSelectionInteractionController(
        ImageSelectionController selection,
        ViewerCursorController cursor,
        ViewerChromeVisibilityController chromeVisibility,
        ViewerFloatingMenuController floatingMenus)
    {
        _selection = selection
            ?? throw new ArgumentNullException(nameof(selection));
        _cursor = cursor
            ?? throw new ArgumentNullException(nameof(cursor));
        _chromeVisibility = chromeVisibility
            ?? throw new ArgumentNullException(nameof(chromeVisibility));
        _floatingMenus = floatingMenus
            ?? throw new ArgumentNullException(nameof(floatingMenus));
    }

    internal void Arm()
    {
        _chromeVisibility.HideControls();
        _selection.Arm();
        UpdateCursor();
    }

    internal void Start(Point position)
    {
        _cursor.Stop();
        _chromeVisibility.HideControls();
        _selection.Start(position);
    }

    internal void Complete()
    {
        _selection.Complete();
    }

    internal void SelectEntireImage()
    {
        _chromeVisibility.HideControls();
        _selection.SelectEntireImage();
        UpdateCursor();
    }

    internal void Cancel()
    {
        _floatingMenus.HideOpenWithAfterAction(
            OpenWithTarget.Selection);
        _selection.Cancel();
        UpdateCursor();
    }

    internal void UpdatePointer(
        Point position,
        bool isPointerPressed)
    {
        _lastPointerPosition = position;
        _isPointerPressed = isPointerPressed;
        UpdateCursor();
    }

    internal void UpdateSelecting(Point position)
    {
        _selection.UpdateSelecting(position);
    }

    internal void Move(Point position)
    {
        _selection.Move(position);
    }

    internal void Resize(Point position)
    {
        _selection.Resize(position);
    }

    internal void PositionToolbar()
    {
        _selection.PositionToolbar();
    }

    internal void ScheduleClipboardPreparation()
    {
        _selection.ScheduleClipboardPreparation();
    }

    internal void CancelClipboardPreparation()
    {
        _selection.CancelClipboardPreparation();
    }

    internal void SetPixelRect(PixelRect pixelRect)
    {
        _selection.SetPixelRect(pixelRect);
    }

    internal Rect GetVisibleImageRect()
    {
        return _selection.GetVisibleImageRect();
    }

    private void UpdateCursor()
    {
        _cursor.UpdateSelection(
            _lastPointerPosition,
            _isPointerPressed);
    }
}
