using Avalonia;
using Avalonia.Input;

using Pica.Viewer.Services;
using Pica.Viewer.ViewModels;

namespace Pica.Viewer.Views;

internal sealed class ViewerPointerInputController
{
    internal Point LastPointerPosition =>
        _lastPointerPosition;

    private const double WheelZoomBase = 1.0015d;

    private readonly ImageViewerView _view;
    private readonly ImageViewerSettingsViewModel _settings;
    private readonly ImageViewportController _viewport;
    private readonly ImageSelectionController _selection;
    private readonly ViewerSelectionInteractionController
        _selectionInteraction;
    private readonly ViewerFloatingMenuController _floatingMenus;
    private readonly ViewerChromeVisibilityController _chromeVisibility;
    private readonly ViewerCursorController _cursor;
    private readonly ViewerWindowModeController _windowMode;
    private readonly ImageDoubleClickTracker _doubleClickTracker;
    private bool _isPointerPressed;
    private bool _isImageClickCandidate;
    private Point _pointerPressPosition;
    private Point _lastPointerPosition;
    private PixelPoint? _lastPointerScreenPosition;

    internal ViewerPointerInputController(
        ImageViewerView view,
        ImageViewerSettingsViewModel settings,
        ImageViewportController viewport,
        ImageSelectionController selection,
        ViewerSelectionInteractionController selectionInteraction,
        ViewerFloatingMenuController floatingMenus,
        ViewerChromeVisibilityController chromeVisibility,
        ViewerCursorController cursor,
        ViewerWindowModeController windowMode)
    {
        _view = view ?? throw new ArgumentNullException(nameof(view));
        _settings = settings
            ?? throw new ArgumentNullException(nameof(settings));
        _viewport = viewport
            ?? throw new ArgumentNullException(nameof(viewport));
        _selection = selection
            ?? throw new ArgumentNullException(nameof(selection));
        _selectionInteraction = selectionInteraction
            ?? throw new ArgumentNullException(
                nameof(selectionInteraction));
        _floatingMenus = floatingMenus
            ?? throw new ArgumentNullException(nameof(floatingMenus));
        _chromeVisibility = chromeVisibility
            ?? throw new ArgumentNullException(nameof(chromeVisibility));
        _cursor = cursor
            ?? throw new ArgumentNullException(nameof(cursor));
        _windowMode = windowMode
            ?? throw new ArgumentNullException(nameof(windowMode));
        _doubleClickTracker = new ImageDoubleClickTracker();
    }

    internal void ResetDoubleClickTracking()
    {
        _doubleClickTracker.Reset();
    }

    internal void OnPointerPressed(
        object? sender,
        PointerPressedEventArgs e)
    {
        _ = sender;

        PointerPoint point =
            e.GetCurrentPoint(_view.ViewerArea);
        Point position = point.Position;
        _lastPointerScreenPosition =
            VisualExtensions.PointToScreen(
                _view.Root,
                e.GetPosition(_view.Root));
        _chromeVisibility.SetControlModifierActive(
            ViewerInputModifiers.IsControlPressed(
                e.KeyModifiers));
        _cursor.Show();
        _selectionInteraction.UpdatePointer(
            position,
            _isPointerPressed);

        if (point.Properties.IsRightButtonPressed)
        {
            _floatingMenus.ShowContext(position);
            e.Handled = true;
            return;
        }

        bool isLeftButtonPressed =
            point.Properties.IsLeftButtonPressed;
        bool isMiddleButtonPressed =
            point.Properties.IsMiddleButtonPressed;

        if (!isLeftButtonPressed && !isMiddleButtonPressed)
        {
            return;
        }

        _floatingMenus.HideAll();
        _isPointerPressed = true;
        _isImageClickCandidate = false;
        _pointerPressPosition = position;

        if (_selection.IsArmed && isLeftButtonPressed)
        {
            StartPointerSelection(position, e);
            return;
        }

        if (_selection.IsActive)
        {
            HandleSelectionPointerPressed(
                position,
                isLeftButtonPressed,
                isMiddleButtonPressed,
                e);
            return;
        }

        if (isLeftButtonPressed
            && ViewerInputModifiers.IsControlPressed(
                e.KeyModifiers))
        {
            StartPointerSelection(position, e);
            return;
        }

        _isImageClickCandidate = isLeftButtonPressed
            && _selectionInteraction
                .GetVisibleImageRect()
                .Contains(position);
        _viewport.BeginPanMotion(position);
        e.Pointer.Capture(_view.ViewerArea);
        e.Handled = true;
    }

    internal void OnPointerMoved(
        object? sender,
        PointerEventArgs e)
    {
        _ = sender;

        Point position = e.GetPosition(_view.ViewerArea);
        PixelPoint screenPosition =
            VisualExtensions.PointToScreen(
                _view.Root,
                e.GetPosition(_view.Root));
        bool hasPointerMoved = ViewerPointerMotion.HasMoved(
            _lastPointerScreenPosition,
            screenPosition);
        _lastPointerScreenPosition = screenPosition;
        _lastPointerPosition = position;
        _chromeVisibility.SetControlModifierActive(
            ViewerInputModifiers.IsControlPressed(
                e.KeyModifiers));

        if (hasPointerMoved)
        {
            _cursor.Show();
            _chromeVisibility.Update(position);
            _selectionInteraction.UpdatePointer(
                position,
                _isPointerPressed);
        }

        if (_isImageClickCandidate
            && HasPointerMovedPastClickTolerance(position))
        {
            _isImageClickCandidate = false;
        }

        if (_selection.IsSelecting)
        {
            _selectionInteraction.UpdateSelecting(position);
            e.Handled = true;
            return;
        }

        if (_viewport.IsPanning)
        {
            _viewport.MovePanMotion(
                position,
                e.KeyModifiers);
            e.Handled = true;
            return;
        }

        if (_selection.IsActive && _isPointerPressed)
        {
            HandleSelectionPointerMoved(position);
            e.Handled = true;
        }
    }

    internal void OnPointerReleased(
        object? sender,
        PointerReleasedEventArgs e)
    {
        _ = sender;

        Point position = e.GetPosition(_view.ViewerArea);
        bool isImageClick = _isImageClickCandidate
            && !_selection.IsSelecting
            && !_selection.IsActive
            && _selectionInteraction
                .GetVisibleImageRect()
                .Contains(position);
        bool wasPanning = _viewport.IsPanning;
        _isPointerPressed = false;
        _isImageClickCandidate = false;
        _selection.EndManipulation();
        e.Pointer.Capture(null);

        if (wasPanning)
        {
            _viewport.ReleasePanMotion();
        }

        if (_selection.IsSelecting)
        {
            _selectionInteraction.Complete();
            e.Handled = true;
        }
        else if (_selection.IsActive)
        {
            _view.SelectionToolbar.IsVisible = true;
            _selectionInteraction.PositionToolbar();
            _selectionInteraction.ScheduleClipboardPreparation();
            e.Handled = true;
        }
        else if (isImageClick)
        {
            RegisterImageClick(position);
            e.Handled = true;
        }
    }

    internal void OnPointerWheelChanged(
        object? sender,
        PointerWheelEventArgs e)
    {
        _ = sender;

        int multiplier = GetEffectiveZoomSpeed(
            e.KeyModifiers);
        double factor = Math.Pow(
            WheelZoomBase,
            e.Delta.Y * 120d * multiplier);
        _viewport.BeginScaleAnimation(
            _viewport.Scale * factor,
            e.GetPosition(_view.ViewerArea));
        e.Handled = true;
    }

    internal void OnRootPointerExited(
        object? sender,
        PointerEventArgs e)
    {
        _ = sender;
        _ = e;

        if (_view.Root.IsPointerOver)
        {
            return;
        }

        _chromeVisibility.HideControls();
        _lastPointerScreenPosition = null;
        _cursor.ResetAfterPointerExit();
    }

    private int GetEffectiveZoomSpeed(
        KeyModifiers modifiers)
    {
        return ViewerInputModifiers
            .IsBaseZoomSpeedRequested(modifiers)
            ? ViewerSettingsDefaults.MinimumSpeed
            : _settings.ZoomSpeed;
    }

    private void HandleSelectionPointerPressed(
        Point position,
        bool isLeftButtonPressed,
        bool isMiddleButtonPressed,
        PointerPressedEventArgs e)
    {
        if (isMiddleButtonPressed)
        {
            _viewport.BeginPanMotion(position);
            CaptureSelectionPointer(position, e);
            return;
        }

        if (!isLeftButtonPressed)
        {
            e.Handled = true;
            return;
        }

        _selection.BeginManipulation(position);
        CaptureSelectionPointer(position, e);
    }

    private void HandleSelectionPointerMoved(Point position)
    {
        if (_selection.IsMoving)
        {
            _selectionInteraction.Move(position);
        }
        else if (_selection.ResizeMode
            != SelectionResizeModes.None)
        {
            _selectionInteraction.Resize(position);
        }
    }

    private void StartPointerSelection(
        Point position,
        PointerPressedEventArgs e)
    {
        _selectionInteraction.Start(position);
        e.Pointer.Capture(_view.ViewerArea);
        e.Handled = true;
    }

    private void CaptureSelectionPointer(
        Point position,
        PointerPressedEventArgs e)
    {
        _view.SelectionToolbar.IsVisible = false;
        _selectionInteraction.UpdatePointer(
            position,
            _isPointerPressed);
        e.Pointer.Capture(_view.ViewerArea);
        e.Handled = true;
    }

    private bool HasPointerMovedPastClickTolerance(
        Point position)
    {
        return !_doubleClickTracker.IsWithinMovementTolerance(
            _pointerPressPosition,
            position);
    }

    private void RegisterImageClick(Point position)
    {
        DateTimeOffset clickedAt = DateTimeOffset.UtcNow;

        if (_settings.ExpandOnDoubleClick
            && _doubleClickTracker.RegisterClick(
                position,
                clickedAt))
        {
            _windowMode.Toggle();
        }
    }
}
