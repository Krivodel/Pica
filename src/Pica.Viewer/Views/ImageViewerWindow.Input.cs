using Avalonia;
using Avalonia.Input;
using SukiUI.Controls;

using Pica.Viewer.Services;

namespace Pica.Viewer.Views;

public sealed partial class ImageViewerWindow : SukiWindow
{
    private static bool IsAnyModifierPressed(
        KeyModifiers modifiers,
        KeyModifiers requestedModifiers)
    {
        return (modifiers & requestedModifiers) != KeyModifiers.None;
    }

    private static bool IsControlModifierPressed(KeyModifiers modifiers)
    {
        return IsAnyModifierPressed(modifiers, KeyModifiers.Control);
    }

    private static bool IsControlKey(Key key)
    {
        return (key == Key.LeftCtrl) || (key == Key.RightCtrl);
    }

    private static bool IsBaseZoomSpeedRequested(KeyModifiers modifiers)
    {
        return IsAnyModifierPressed(
            modifiers,
            KeyModifiers.Shift | KeyModifiers.Alt | KeyModifiers.Control);
    }

    private static bool IsBaseMovementSpeedRequested(KeyModifiers modifiers)
    {
        return IsAnyModifierPressed(
            modifiers,
            KeyModifiers.Shift | KeyModifiers.Alt);
    }

    private static bool TryGetNavigationDirection(
        Key key,
        out int direction)
    {
        if ((key == Key.Left) || (key == Key.A))
        {
            direction = -1;
            return true;
        }

        if ((key == Key.Right) || (key == Key.D))
        {
            direction = 1;
            return true;
        }

        direction = 0;
        return false;
    }

    private int GetEffectiveMovementSpeed(KeyModifiers modifiers)
    {
        return IsBaseMovementSpeedRequested(modifiers)
            ? ViewerSettingsDefaults.MinimumSpeed
            : _settings.MovementSpeed;
    }

    private int GetEffectiveZoomSpeed(KeyModifiers modifiers)
    {
        return IsBaseZoomSpeedRequested(modifiers)
            ? ViewerSettingsDefaults.MinimumSpeed
            : _settings.ZoomSpeed;
    }

    private double GetZoomButtonFactor()
    {
        return Math.Pow(ZoomButtonStepBase, GetEffectiveZoomSpeed(_activeKeyModifiers));
    }

    private ImagePanMotionMode GetPanMotionMode()
    {
        return _settings.IsPanningInertiaEnabled
            ? ImagePanMotionMode.SmoothWithInertia
            : ImagePanMotionMode.Smooth;
    }

    private void BeginPanMotion(Point pointerPosition)
    {
        StopScaleAnimation();
        ApplyImageLayout();
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        _lastPointerPosition = pointerPosition;
        _lastPanAnimationFrameTimestamp = timestamp;
        _panMotion.Begin(new Point(_offsetX, _offsetY), timestamp);
    }

    private void MovePanMotion(Point pointerPosition, KeyModifiers modifiers)
    {
        if (!TryGetCurrentPanBounds(out Rect bounds))
        {
            return;
        }

        Vector pointerDelta = pointerPosition - _lastPointerPosition;
        int multiplier = GetEffectiveMovementSpeed(modifiers);
        Vector imageDelta = pointerDelta * multiplier;
        _lastPointerPosition = pointerPosition;
        _panMotion.Move(
            imageDelta,
            bounds,
            DateTimeOffset.UtcNow);
        ApplyPanMotionOffset();
        SchedulePanAnimationFrame();
    }

    private void ReleasePanMotion()
    {
        _panMotion.Release(GetPanMotionMode(), DateTimeOffset.UtcNow);
        ApplyPanMotionOffset();
        SchedulePanAnimationFrame();
    }

    private void ResetPanMotion()
    {
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        Point offset = new(_offsetX, _offsetY);
        _lastPanAnimationFrameTimestamp = timestamp;

        if (_isPanning)
        {
            _panMotion.Begin(offset, timestamp);
            return;
        }

        _panMotion.Reset(offset);
    }

    private void StopPanMotion()
    {
        _isPanning = false;
        _isPanAnimationFramePending = false;
        _panMotion.Reset(new Point(_offsetX, _offsetY));
    }

    private void ApplyPanMotionOffset()
    {
        _offsetX = _panMotion.CurrentOffset.X;
        _offsetY = _panMotion.CurrentOffset.Y;
        ApplyImageLayout();
    }

    private void SchedulePanAnimationFrame()
    {
        if (!_panMotion.IsActive || _isPanAnimationFramePending)
        {
            return;
        }

        _isPanAnimationFramePending = true;
        RequestViewerAnimationFrame(OnPanAnimationFrame);
    }

    private void OnPanAnimationFrame(TimeSpan frameTime)
    {
        _ = frameTime;

        _isPanAnimationFramePending = false;

        if (!_panMotion.IsActive)
        {
            return;
        }

        if (!TryGetCurrentPanBounds(out Rect bounds))
        {
            ResetPanMotion();
            return;
        }

        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        TimeSpan elapsed = timestamp - _lastPanAnimationFrameTimestamp;
        _lastPanAnimationFrameTimestamp = timestamp;
        _panMotion.Advance(elapsed, GetPanMotionMode(), bounds);
        ApplyPanMotionOffset();
        SchedulePanAnimationFrame();
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _ = sender;

        PointerPoint point = e.GetCurrentPoint(_view.ViewerArea);
        Point position = point.Position;
        _lastPointerScreenPosition = VisualExtensions.PointToScreen(
            _view.Root,
            e.GetPosition(_view.Root));
        _isControlModifierActive = IsControlModifierPressed(e.KeyModifiers);
        ShowCursor();
        UpdateSelectionCursor(position);

        if (point.Properties.IsRightButtonPressed)
        {
            ShowContextMenu(position);
            e.Handled = true;
            return;
        }

        bool isLeftButtonPressed = point.Properties.IsLeftButtonPressed;
        bool isMiddleButtonPressed = point.Properties.IsMiddleButtonPressed;

        if (!isLeftButtonPressed && !isMiddleButtonPressed)
        {
            return;
        }

        HideContextMenu();
        HideToolMenu();
        _isPointerPressed = true;
        _isImageClickCandidate = false;
        _pointerPressPosition = position;

        if (_isSelectionArmed && isLeftButtonPressed)
        {
            StartPointerSelection(position, e);
            return;
        }

        if (_isSelectionActive)
        {
            if (isMiddleButtonPressed)
            {
                _isPanning = true;
                BeginPanMotion(position);
                CaptureSelectionPointer(position, e);
                return;
            }

            if (!isLeftButtonPressed)
            {
                e.Handled = true;
                return;
            }

            _selectionStartRect = NormalizeSelectionRect(_selectionRect);
            _selectionStartPixelRect = _selectionPixelRect;
            CancelSelectionClipboardPreparation();
            _selectionResizeMode = GetSelectionResizeMode(position);
            _isSelectionMoving = (_selectionResizeMode == SelectionResizeMode.None)
                && _selectionStartRect.Contains(position);
            CaptureSelectionPointer(position, e);
            return;
        }

        if (isLeftButtonPressed && IsControlModifierPressed(e.KeyModifiers))
        {
            StartPointerSelection(position, e);
            return;
        }

        _isImageClickCandidate = isLeftButtonPressed && GetVisibleImageRect().Contains(position);
        _isPanning = true;
        BeginPanMotion(position);
        e.Pointer.Capture(_view.ViewerArea);
        e.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        _ = sender;

        Point position = e.GetPosition(_view.ViewerArea);
        PixelPoint screenPosition = VisualExtensions.PointToScreen(
            _view.Root,
            e.GetPosition(_view.Root));
        bool hasPointerMoved = ViewerPointerMotion.HasMoved(
            _lastPointerScreenPosition,
            screenPosition);
        _lastPointerScreenPosition = screenPosition;
        _lastPointerHoverPosition = position;
        _isControlModifierActive = IsControlModifierPressed(e.KeyModifiers);

        if (hasPointerMoved)
        {
            ShowCursor();
            UpdateControlVisibility(position);
            UpdateSelectionCursor(position);
        }

        if (_isImageClickCandidate && HasPointerMovedPastClickTolerance(position))
        {
            _isImageClickCandidate = false;
        }

        if (_isSelecting)
        {
            UpdateSelecting(position);
            e.Handled = true;
            return;
        }

        if (_isPanning)
        {
            MovePanMotion(position, e.KeyModifiers);
            e.Handled = true;
            return;
        }

        if (_isSelectionActive && _isPointerPressed)
        {
            if (_isSelectionMoving)
            {
                MoveSelection(position);
            }
            else if (_selectionResizeMode != SelectionResizeMode.None)
            {
                ResizeSelection(position);
            }

            e.Handled = true;
            return;
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _ = sender;

        Point position = e.GetPosition(_view.ViewerArea);
        bool isImageClick = _isImageClickCandidate
            && !_isSelecting
            && !_isSelectionActive
            && GetVisibleImageRect().Contains(position);
        bool wasPanning = _isPanning;
        _isPointerPressed = false;
        _isPanning = false;
        _isImageClickCandidate = false;
        _isSelectionMoving = false;
        _selectionResizeMode = SelectionResizeMode.None;
        e.Pointer.Capture(null);

        if (wasPanning)
        {
            ReleasePanMotion();
        }

        if (_isSelecting)
        {
            CompleteSelection();
            e.Handled = true;
        }
        else if (_isSelectionActive)
        {
            _view.SelectionToolbar.IsVisible = true;
            PositionSelectionToolbar();
            ScheduleSelectionClipboardPreparation();
            e.Handled = true;
        }
        else if (isImageClick)
        {
            RegisterImageClick(position);
            e.Handled = true;
        }
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        _ = sender;

        int multiplier = GetEffectiveZoomSpeed(e.KeyModifiers);
        double factor = Math.Pow(WheelZoomBase, e.Delta.Y * 120d * multiplier);
        BeginScaleAnimation(_scale * factor, e.GetPosition(_view.ViewerArea));
        e.Handled = true;
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        _ = sender;

        if (e.Key != Key.Tab)
        {
            return;
        }

        if (!_isImageOperationRunning)
        {
            ToggleChannelMode();
        }

        e.Handled = true;
    }

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        _ = sender;

        if (_isImageOperationRunning)
        {
            e.Handled = true;
            return;
        }

        _activeKeyModifiers = e.KeyModifiers;
        _isControlModifierActive = IsControlModifierPressed(e.KeyModifiers) || IsControlKey(e.Key);
        if (_isControlModifierActive)
        {
            HideViewerControls();
        }

        if (e.Key == Key.Escape)
        {
            if (_view.SettingsPanel.IsVisible)
            {
                HideSettingsPanel();
            }
            else if (_isSelectionActive || _isSelectionArmed)
            {
                CancelSelection();
            }
            else if (_channelSelection.IsActive)
            {
                ExitChannelMode();
            }
            else
            {
                CloseWithFade();
            }

            e.Handled = true;
            return;
        }

        if ((_isSelectionActive || _isSelectionArmed)
            && (e.Key == Key.A)
            && IsControlModifierPressed(e.KeyModifiers))
        {
            SelectEntireImage();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F)
        {
            await ToggleFilteringAsync();
            e.Handled = true;
            return;
        }

        if (_isSelectionActive)
        {
            if ((e.Key == Key.C) && IsControlModifierPressed(e.KeyModifiers))
            {
                await CopySelectionAndCloseAsync(CancellationToken.None);
                e.Handled = true;
            }
            else if (_channelSelection.IsActive
                && TryGetNavigationDirection(
                    e.Key,
                    out int channelDirection))
            {
                Navigate(channelDirection);
                e.Handled = true;
            }

            return;
        }

        if (e.Key == Key.Space)
        {
            BeginResetScaleAndCenterAnimation();
            e.Handled = true;
        }
        else if (TryGetNavigationDirection(
            e.Key,
            out int navigationDirection))
        {
            Navigate(navigationDirection);
            e.Handled = true;
        }
        else if ((e.Key == Key.C) && IsControlModifierPressed(e.KeyModifiers))
        {
            await CopyCurrentImageWithFeedbackAsync(CancellationToken.None);
            e.Handled = true;
        }
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        _ = sender;

        _activeKeyModifiers = e.KeyModifiers;
        _isControlModifierActive = IsControlModifierPressed(e.KeyModifiers);
        if (_isControlModifierActive)
        {
            HideViewerControls();
            return;
        }

        UpdateControlVisibility(_lastPointerHoverPosition);
    }

    private void StartPointerSelection(Point position, PointerPressedEventArgs e)
    {
        StartSelection(position);
        e.Pointer.Capture(_view.ViewerArea);
        e.Handled = true;
    }

    private void CaptureSelectionPointer(Point position, PointerPressedEventArgs e)
    {
        _view.SelectionToolbar.IsVisible = false;
        UpdateSelectionCursor(position);
        e.Pointer.Capture(_view.ViewerArea);
        e.Handled = true;
    }

    private bool HasPointerMovedPastClickTolerance(Point position)
    {
        return !_imageDoubleClickTracker.IsWithinMovementTolerance(_pointerPressPosition, position);
    }

    private void RegisterImageClick(Point position)
    {
        DateTimeOffset clickedAt = DateTimeOffset.UtcNow;

        if (_settings.ExpandOnDoubleClick
            && _imageDoubleClickTracker.RegisterClick(position, clickedAt))
        {
            ToggleWindowMode();
        }
    }
}
