using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;

using Pica.Viewer.Resources;
using Pica.Viewer.Services;
using Pica.Viewer.ViewModels;

namespace Pica.Viewer.Views;

internal sealed class ImageSelectionController : IDisposable
{
    internal bool IsSelecting => _isSelecting;
    internal bool IsActive => _isSelectionActive;
    internal bool IsMoving => _isSelectionMoving;
    internal bool IsArmed => _isSelectionArmed;
    internal SelectionResizeModes ResizeMode => _selectionResizeMode;
    internal Rect SelectionRect => _selectionRect;
    internal PixelRect PixelRect => _selectionPixelRect;

    private const double MinimumSelectionSize = 12d;

    private Bitmap? CurrentBitmap => _viewport.CurrentBitmap;

    private readonly ImageViewerView _view;
    private readonly ImageViewportController _viewport;
    private readonly ViewerFrameAnimationRunner _animationRunner;
    private readonly ImageSelectionGeometry _geometry;
    private readonly ImageSelectionClipboardPreparation
        _clipboardPreparation;
    private bool _isSelecting;
    private bool _isSelectionActive;
    private bool _isSelectionMoving;
    private bool _isSelectionArmed;
    private SelectionResizeModes _selectionResizeMode;
    private Point _pointerPressPosition;
    private Point _selectionStartPosition;
    private Rect _selectionRect;
    private Rect _selectionStartRect;
    private PixelRect _selectionPixelRect;
    private PixelRect _selectionStartPixelRect;
    private long _selectionOverlayAnimationId;

    internal ImageSelectionController(
        TopLevel topLevel,
        ImageViewerView view,
        ImageViewerActionsViewModel actions,
        ImagePresentationController imagePresentation,
        ImageViewportController viewport,
        ViewerFrameAnimationRunner animationRunner)
    {
        ArgumentNullException.ThrowIfNull(topLevel);
        _view = view ?? throw new ArgumentNullException(nameof(view));
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(imagePresentation);
        _viewport = viewport
            ?? throw new ArgumentNullException(nameof(viewport));
        _animationRunner = animationRunner
            ?? throw new ArgumentNullException(nameof(animationRunner));
        _geometry = new ImageSelectionGeometry(
            topLevel,
            viewport);
        _clipboardPreparation =
            new ImageSelectionClipboardPreparation(
                actions,
                imagePresentation,
                viewport);
        _viewport.LayoutChanged += OnViewportLayoutChanged;
    }

    public void Dispose()
    {
        _viewport.LayoutChanged -= OnViewportLayoutChanged;
        _clipboardPreparation.Dispose();
    }

    internal static Rect NormalizeSelectionRect(Rect rect)
    {
        return ImageSelectionGeometry.NormalizeRect(rect);
    }

    internal void Arm()
    {
        _isSelectionArmed = true;
    }

    internal void Start(Point position)
    {
        CancelClipboardPreparation();
        _isSelectionArmed = false;
        _isSelecting = true;
        _isSelectionActive = true;
        _selectionStartPosition = ClampPointToImage(position);
        _selectionRect = new Rect(_selectionStartPosition, _selectionStartPosition);
        _selectionPixelRect = new PixelRect();
        _view.SelectionToolbar.IsVisible = false;
        UpdateOverlay();
        ShowSelectionOverlay();
    }

    internal void UpdateSelecting(Point position)
    {
        Point clampedPosition = ClampPointToImage(position);
        double left = Math.Min(_selectionStartPosition.X, clampedPosition.X);
        double top = Math.Min(_selectionStartPosition.Y, clampedPosition.Y);
        double right = Math.Max(_selectionStartPosition.X, clampedPosition.X);
        double bottom = Math.Max(_selectionStartPosition.Y, clampedPosition.Y);
        SetSelectionFromScreenRect(new Rect(left, top, right - left, bottom - top));
        UpdateOverlay();
    }

    internal void Complete()
    {
        _isSelecting = false;

        if ((_selectionRect.Width < MinimumSelectionSize) || (_selectionRect.Height < MinimumSelectionSize))
        {
            Cancel();
            return;
        }

        _view.SelectionToolbar.IsVisible = true;
        PositionToolbar();
        ScheduleClipboardPreparation();
    }

    internal void SelectEntireImage()
    {
        if (CurrentBitmap is null)
        {
            return;
        }

        _isSelectionArmed = false;
        _isSelecting = false;
        _isSelectionActive = true;
        _isSelectionMoving = false;
        _selectionResizeMode = SelectionResizeModes.None;
        SetPixelRect(new PixelRect(
            0,
            0,
            CurrentBitmap.PixelSize.Width,
            CurrentBitmap.PixelSize.Height));
        _view.SelectionToolbar.IsVisible = true;
        ShowSelectionOverlay();
        UpdateOverlay();
        ScheduleClipboardPreparation();
    }

    internal void Cancel()
    {
        CancelClipboardPreparation();
        _isSelecting = false;
        _isSelectionArmed = false;
        _isSelectionActive = false;
        _isSelectionMoving = false;
        _selectionResizeMode = SelectionResizeModes.None;
        _view.SelectionToolbar.IsVisible = false;
        HideSelectionOverlay();
    }

    internal void RefreshAfterDisplayedBitmapChange()
    {
        if (!_isSelectionActive)
        {
            return;
        }

        SetPixelRect(_selectionPixelRect);
        UpdateOverlay();
        ScheduleClipboardPreparation();
    }

    internal Point ClampPointToImage(Point position)
    {
        return _geometry.ClampPointToImage(position);
    }

    internal Rect GetVisibleImageRect()
    {
        return _viewport.GetVisibleImageRect();
    }

    internal void UpdateOverlay()
    {
        Size viewport = GetViewportSize();
        Rect rect = NormalizeSelectionRect(_selectionRect);
        _view.SelectionShade.Data = CreateSelectionShadeGeometry(viewport, rect);
        _view.SelectionFrame.Data = new RectangleGeometry(rect);
        PositionToolbar();
    }

    internal void PositionToolbar()
    {
        if (!_view.SelectionToolbar.IsVisible)
        {
            return;
        }

        Size viewport = GetViewportSize();
        Rect rect = NormalizeSelectionRect(_selectionRect);
        Point position = ImageSelectionGeometry.GetToolbarPosition(
            rect,
            viewport,
            _view.SelectionToolbar.Width);
        Canvas.SetLeft(_view.SelectionToolbar, position.X);
        Canvas.SetTop(_view.SelectionToolbar, position.Y);
    }

    internal SelectionResizeModes GetResizeMode(Point position)
    {
        return _geometry.GetResizeMode(
            _selectionRect,
            position);
    }

    internal void UpdateCursor(
        Point position,
        bool isPointerPressed,
        bool isPanning,
        bool isCursorHidden,
        Action<Cursor> setCursor)
    {
        ArgumentNullException.ThrowIfNull(setCursor);

        if (_isSelecting || _isSelectionArmed)
        {
            setCursor(ViewerCursors.Crosshair);
            return;
        }

        if (!_isSelectionActive)
        {
            if (!isCursorHidden)
            {
                setCursor(ViewerCursors.Arrow);
            }

            return;
        }

        if (isPointerPressed)
        {
            if (isPanning)
            {
                setCursor(ViewerCursors.Move);
                return;
            }

            if (_isSelectionMoving)
            {
                setCursor(ViewerCursors.Move);
                return;
            }

            if (_selectionResizeMode != SelectionResizeModes.None)
            {
                setCursor(
                    GetSelectionResizeCursor(_selectionResizeMode));
                return;
            }

            setCursor(ViewerCursors.Arrow);
            return;
        }

        SelectionResizeModes resizeMode = GetResizeMode(position);
        if (resizeMode != SelectionResizeModes.None)
        {
            setCursor(GetSelectionResizeCursor(resizeMode));
            return;
        }

        Rect rect = NormalizeSelectionRect(_selectionRect);
        setCursor(rect.Contains(position)
            ? ViewerCursors.Move
            : ViewerCursors.Arrow);
    }

    internal void BeginManipulation(Point position)
    {
        _pointerPressPosition = position;
        _selectionStartRect =
            NormalizeSelectionRect(_selectionRect);
        _selectionStartPixelRect = _selectionPixelRect;
        CancelClipboardPreparation();
        _selectionResizeMode = GetResizeMode(position);
        _isSelectionMoving =
            (_selectionResizeMode == SelectionResizeModes.None)
            && _selectionStartRect.Contains(position);
    }

    internal void EndManipulation()
    {
        _isSelectionMoving = false;
        _selectionResizeMode = SelectionResizeModes.None;
    }

    internal void Move(Point position)
    {
        Vector delta = position - _pointerPressPosition;
        int deltaX =
            _geometry.ScreenDeltaToPixelDelta(delta.X);
        int deltaY =
            _geometry.ScreenDeltaToPixelDelta(delta.Y);
        int maxLeft = Math.Max(
            0,
            CurrentBitmap?.PixelSize.Width - _selectionStartPixelRect.Width ?? 0);
        int maxTop = Math.Max(
            0,
            CurrentBitmap?.PixelSize.Height - _selectionStartPixelRect.Height ?? 0);
        int left = Math.Clamp(_selectionStartPixelRect.X + deltaX, 0, maxLeft);
        int top = Math.Clamp(_selectionStartPixelRect.Y + deltaY, 0, maxTop);
        SetPixelRect(new PixelRect(
            left,
            top,
            _selectionStartPixelRect.Width,
            _selectionStartPixelRect.Height));
        UpdateOverlay();
    }

    internal void Resize(Point position)
    {
        if (CurrentBitmap is null)
        {
            return;
        }

        int left = _selectionStartPixelRect.X;
        int top = _selectionStartPixelRect.Y;
        int right = _selectionStartPixelRect.X + _selectionStartPixelRect.Width;
        int bottom = _selectionStartPixelRect.Y + _selectionStartPixelRect.Height;
        int minimumPixelSize =
            _geometry.GetMinimumPixelSize();

        if (_selectionResizeMode.HasFlag(SelectionResizeModes.Left))
        {
            left = Math.Clamp(
                _geometry.ScreenXToPixelBoundary(position.X),
                0,
                right - minimumPixelSize);
        }

        if (_selectionResizeMode.HasFlag(SelectionResizeModes.Right))
        {
            right = Math.Clamp(
                _geometry.ScreenXToPixelBoundary(position.X),
                left + minimumPixelSize,
                CurrentBitmap.PixelSize.Width);
        }

        if (_selectionResizeMode.HasFlag(SelectionResizeModes.Top))
        {
            top = Math.Clamp(
                _geometry.ScreenYToPixelBoundary(position.Y),
                0,
                bottom - minimumPixelSize);
        }

        if (_selectionResizeMode.HasFlag(SelectionResizeModes.Bottom))
        {
            bottom = Math.Clamp(
                _geometry.ScreenYToPixelBoundary(position.Y),
                top + minimumPixelSize,
                CurrentBitmap.PixelSize.Height);
        }

        SetPixelRect(new PixelRect(
            left,
            top,
            right - left,
            bottom - top));
        UpdateOverlay();
    }

    internal PixelRect? GetNormalizedPixelRect()
    {
        return CurrentBitmap is null
            ? null
            : BitmapPixelCopy.NormalizeSourceRect(
                CurrentBitmap.PixelSize,
                _selectionPixelRect);
    }

    internal void ScheduleClipboardPreparation()
    {
        _clipboardPreparation.Schedule(
            GetNormalizedPixelRect());
    }

    internal void CancelClipboardPreparation()
    {
        _clipboardPreparation.Cancel();
    }

    internal async Task<PreparedClipboardImage?> GetPreparedClipboardImageAsync(
        CancellationToken ct)
    {
        return await _clipboardPreparation.GetAsync(
            GetNormalizedPixelRect(),
            ct);
    }

    internal void SetPixelRect(PixelRect pixelRect)
    {
        _selectionPixelRect = pixelRect;
        _selectionRect = _geometry.GetScreenRect(pixelRect);
    }

    private static void IgnoreAnimationProgress(double progress)
    {
        _ = progress;
    }

    private static Geometry CreateSelectionShadeGeometry(
        Size viewport,
        Rect selectionRect)
    {
        GeometryGroup geometry = new()
        {
            FillRule = FillRule.EvenOdd
        };
        geometry.Children.Add(
            new RectangleGeometry(
                new Rect(
                    0d,
                    0d,
                    viewport.Width,
                    viewport.Height)));
        geometry.Children.Add(
            new RectangleGeometry(selectionRect));

        return geometry;
    }

    private static Cursor GetSelectionResizeCursor(
        SelectionResizeModes resizeMode)
    {
        return resizeMode switch
        {
            SelectionResizeModes.Left
                or SelectionResizeModes.Right =>
                    ViewerCursors.HorizontalResize,
            SelectionResizeModes.Top
                or SelectionResizeModes.Bottom =>
                    ViewerCursors.VerticalResize,
            SelectionResizeModes.TopLeft
                or SelectionResizeModes.BottomRight =>
                    ViewerCursors.TopLeftResize,
            SelectionResizeModes.TopRight
                or SelectionResizeModes.BottomLeft =>
                    ViewerCursors.TopRightResize,
            _ => ViewerCursors.Arrow
        };
    }

    private void ShowSelectionOverlay()
    {
        _selectionOverlayAnimationId++;
        _view.SelectionOverlay.IsVisible = true;
        _view.SelectionShade.Opacity =
            _view.VisibleControlsOpacity;
        _view.SelectionFrame.Opacity =
            _view.VisibleControlsOpacity;
    }

    private void HideSelectionOverlay()
    {
        long animationId = ++_selectionOverlayAnimationId;
        _view.SelectionShade.Opacity =
            _view.HiddenControlsOpacity;
        _view.SelectionFrame.Opacity =
            _view.HiddenControlsOpacity;
        _animationRunner.Start(
            ImageViewerVisualMetrics.SelectionOverlayFadeDuration,
            () => (animationId
                    == _selectionOverlayAnimationId)
                && !_isSelectionActive
                && !_isSelecting,
            IgnoreAnimationProgress,
            completed: () =>
            {
                _view.SelectionOverlay.IsVisible = false;
            });
    }

    private void SetSelectionFromScreenRect(Rect screenRect)
    {
        SetPixelRect(
            _geometry.GetSourcePixelRect(screenRect));
    }

    private Size GetViewportSize()
    {
        return _viewport.GetViewportSize();
    }

    private void OnViewportLayoutChanged(
        object? sender,
        EventArgs e)
    {
        _ = sender;
        _ = e;

        if (_isSelectionActive && !_isSelecting)
        {
            SetPixelRect(_selectionPixelRect);
            UpdateOverlay();
        }
    }
}
