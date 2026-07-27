using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Microsoft.Extensions.Logging;
using SukiUI.Controls;

using Pica.Viewer.Resources;
using Pica.Viewer.Services;

namespace Pica.Viewer.Views;

public sealed partial class ImageViewerWindow : SukiWindow
{
    private void StartSelection(Point position)
    {
        CancelSelectionClipboardPreparation();
        _cursorTimer.Stop();
        HideViewerControls();
        _isSelectionArmed = false;
        _isSelecting = true;
        _isSelectionActive = true;
        _selectionStartPosition = ClampPointToImage(position);
        _selectionRect = new Rect(_selectionStartPosition, _selectionStartPosition);
        _selectionPixelRect = new PixelRect();
        _view.SelectionToolbar.IsVisible = false;
        UpdateSelectionOverlay();
        ShowSelectionOverlay();
    }

    private void UpdateSelecting(Point position)
    {
        Point clampedPosition = ClampPointToImage(position);
        double left = Math.Min(_selectionStartPosition.X, clampedPosition.X);
        double top = Math.Min(_selectionStartPosition.Y, clampedPosition.Y);
        double right = Math.Max(_selectionStartPosition.X, clampedPosition.X);
        double bottom = Math.Max(_selectionStartPosition.Y, clampedPosition.Y);
        SetSelectionFromScreenRect(new Rect(left, top, right - left, bottom - top));
        UpdateSelectionOverlay();
    }

    private void CompleteSelection()
    {
        _isSelecting = false;

        if ((_selectionRect.Width < MinimumSelectionSize) || (_selectionRect.Height < MinimumSelectionSize))
        {
            CancelSelection();
            return;
        }

        _view.SelectionToolbar.IsVisible = true;
        PositionSelectionToolbar();
        ScheduleSelectionClipboardPreparation();
    }

    private void SelectEntireImage()
    {
        if (_bitmap is null)
        {
            return;
        }

        HideViewerControls();
        _isSelectionArmed = false;
        _isSelecting = false;
        _isSelectionActive = true;
        _isSelectionMoving = false;
        _selectionResizeMode = SelectionResizeMode.None;
        SetSelectionPixelRect(new PixelRect(0, 0, _bitmap.PixelSize.Width, _bitmap.PixelSize.Height));
        _view.SelectionToolbar.IsVisible = true;
        ShowSelectionOverlay();
        UpdateSelectionOverlay();
        UpdateSelectionCursor(_lastPointerHoverPosition);
        ScheduleSelectionClipboardPreparation();
    }

    private void CancelSelection()
    {
        CancelSelectionClipboardPreparation();
        HideOpenWithSubmenu();
        _isSelecting = false;
        _isSelectionArmed = false;
        _isSelectionActive = false;
        _isSelectionMoving = false;
        _selectionResizeMode = SelectionResizeMode.None;
        _view.SelectionToolbar.IsVisible = false;
        HideSelectionOverlay();
        UpdateSelectionCursor(_lastPointerHoverPosition);
    }

    private void ShowSelectionOverlay()
    {
        _selectionOverlayAnimationId++;
        _view.SelectionOverlay.IsVisible = true;
        _view.SelectionShade.Opacity = ImageViewerVisualMetrics.VisibleControlsOpacity;
        _view.SelectionFrame.Opacity = ImageViewerVisualMetrics.VisibleControlsOpacity;
    }

    private async void HideSelectionOverlay()
    {
        long animationId = ++_selectionOverlayAnimationId;
        _view.SelectionShade.Opacity = ImageViewerVisualMetrics.HiddenControlsOpacity;
        _view.SelectionFrame.Opacity = ImageViewerVisualMetrics.HiddenControlsOpacity;

        await Task.Delay(ImageViewerVisualMetrics.SelectionOverlayFadeDuration);

        if ((animationId == _selectionOverlayAnimationId)
            && !_isSelectionActive
            && !_isSelecting)
        {
            _view.SelectionOverlay.IsVisible = false;
        }
    }

    private Point ClampPointToImage(Point position)
    {
        Rect imageRect = GetVisibleImageRect();
        double x = Math.Clamp(position.X, imageRect.Left, imageRect.Right);
        double y = Math.Clamp(position.Y, imageRect.Top, imageRect.Bottom);

        return new Point(x, y);
    }

    private Rect GetVisibleImageRect()
    {
        Rect windowRect = new(0d, 0d, _view.ViewerArea.Bounds.Width, _view.ViewerArea.Bounds.Height);
        Rect imageRect = new(_offsetX, _offsetY, GetImageDipWidth(), GetImageDipHeight());

        return imageRect.Intersect(windowRect);
    }

    private void UpdateSelectionOverlay()
    {
        Size viewport = GetViewportSize();
        Rect rect = NormalizeSelectionRect(_selectionRect);
        _view.SelectionShade.Data = CreateSelectionShadeGeometry(viewport, rect);
        _view.SelectionFrame.Data = new RectangleGeometry(rect);
        PositionSelectionToolbar();
    }

    private static Geometry CreateSelectionShadeGeometry(Size viewport, Rect selectionRect)
    {
        GeometryGroup geometry = new()
        {
            FillRule = FillRule.EvenOdd
        };
        geometry.Children.Add(new RectangleGeometry(new Rect(0d, 0d, viewport.Width, viewport.Height)));
        geometry.Children.Add(new RectangleGeometry(selectionRect));

        return geometry;
    }

    private static Rect NormalizeSelectionRect(Rect rect)
    {
        double left = Math.Min(rect.Left, rect.Right);
        double top = Math.Min(rect.Top, rect.Bottom);
        double right = Math.Max(rect.Left, rect.Right);
        double bottom = Math.Max(rect.Top, rect.Bottom);

        return new Rect(left, top, right - left, bottom - top);
    }

    private void PositionSelectionToolbar()
    {
        if (!_view.SelectionToolbar.IsVisible)
        {
            return;
        }

        Size viewport = GetViewportSize();
        Rect rect = NormalizeSelectionRect(_selectionRect);
        Point position = GetToolbarPosition(rect, viewport);
        Canvas.SetLeft(_view.SelectionToolbar, position.X);
        Canvas.SetTop(_view.SelectionToolbar, position.Y);
    }

    private Point GetToolbarPosition(Rect rect, Size viewport)
    {
        double toolbarWidth = _view.SelectionToolbar.Width;
        double maximumToolbarX = Math.Max(0d, viewport.Width - toolbarWidth);
        double maximumToolbarY = Math.Max(
            0d,
            viewport.Height - ImageViewerVisualMetrics.SelectionToolbarHeight);
        double centeredX = Math.Clamp(
            rect.Left + ((rect.Width - toolbarWidth) / 2d),
            0d,
            maximumToolbarX);

        if (rect.Bottom
            + SelectionToolbarGap
            + ImageViewerVisualMetrics.SelectionToolbarHeight
            <= viewport.Height)
        {
            double toolbarY = Math.Clamp(rect.Bottom + SelectionToolbarGap, 0d, maximumToolbarY);

            return new Point(centeredX, toolbarY);
        }

        double centeredY = Math.Clamp(
            rect.Top
                + ((rect.Height - ImageViewerVisualMetrics.SelectionToolbarHeight) / 2d),
            0d,
            maximumToolbarY);

        if (rect.Right + SelectionToolbarGap + toolbarWidth <= viewport.Width)
        {
            return new Point(rect.Right + SelectionToolbarGap, centeredY);
        }

        if (rect.Left - SelectionToolbarGap - toolbarWidth >= 0d)
        {
            return new Point(rect.Left - SelectionToolbarGap - toolbarWidth, centeredY);
        }

        if (rect.Top
            - SelectionToolbarGap
            - ImageViewerVisualMetrics.SelectionToolbarHeight
            >= 0d)
        {
            return new Point(
                centeredX,
                rect.Top
                    - SelectionToolbarGap
                    - ImageViewerVisualMetrics.SelectionToolbarHeight);
        }

        double fallbackY = Math.Clamp(
            Math.Max(
                rect.Top,
                rect.Bottom
                    - ImageViewerVisualMetrics.SelectionToolbarHeight
                    - SelectionToolbarGap),
            0d,
            maximumToolbarY);

        return new Point(centeredX, fallbackY);
    }

    private SelectionResizeMode GetSelectionResizeMode(Point position)
    {
        Rect rect = NormalizeSelectionRect(_selectionRect);
        bool isLeftOfRect = position.X < rect.Left;
        bool isRightOfRect = position.X > rect.Right;
        bool isAboveRect = position.Y < rect.Top;
        bool isBelowRect = position.Y > rect.Bottom;
        SelectionResizeMode outsideResizeMode = GetSelectionResizeMode(
            isLeftOfRect,
            isRightOfRect,
            isAboveRect,
            isBelowRect);

        if (outsideResizeMode != SelectionResizeMode.None)
        {
            return outsideResizeMode;
        }

        bool nearLeft = Math.Abs(position.X - rect.Left) <= SelectionHandleSize;
        bool nearRight = Math.Abs(position.X - rect.Right) <= SelectionHandleSize;
        bool nearTop = Math.Abs(position.Y - rect.Top) <= SelectionHandleSize;
        bool nearBottom = Math.Abs(position.Y - rect.Bottom) <= SelectionHandleSize;

        return GetSelectionResizeMode(
            nearLeft,
            nearRight,
            nearTop,
            nearBottom);
    }

    private static SelectionResizeMode GetSelectionResizeMode(
        bool isLeft,
        bool isRight,
        bool isTop,
        bool isBottom)
    {
        return (isLeft, isRight, isTop, isBottom) switch
        {
            (true, _, true, _) => SelectionResizeMode.TopLeft,
            (_, true, true, _) => SelectionResizeMode.TopRight,
            (_, true, _, true) => SelectionResizeMode.BottomRight,
            (true, _, _, true) => SelectionResizeMode.BottomLeft,
            (true, _, _, _) => SelectionResizeMode.Left,
            (_, true, _, _) => SelectionResizeMode.Right,
            (_, _, true, _) => SelectionResizeMode.Top,
            (_, _, _, true) => SelectionResizeMode.Bottom,
            _ => SelectionResizeMode.None
        };
    }

    private void UpdateSelectionCursor(Point position)
    {
        if (_isSelecting || _isSelectionArmed)
        {
            SetVisibleCursor(ViewerCursors.Crosshair);
            return;
        }

        if (!_isSelectionActive)
        {
            if (!_isCursorHidden)
            {
                SetVisibleCursor(ViewerCursors.Arrow);
            }

            return;
        }

        if (_isPointerPressed)
        {
            if (_isPanning)
            {
                SetVisibleCursor(ViewerCursors.Move);
                return;
            }

            if (_isSelectionMoving)
            {
                SetVisibleCursor(ViewerCursors.Move);
                return;
            }

            if (_selectionResizeMode != SelectionResizeMode.None)
            {
                SetVisibleCursor(GetSelectionResizeCursor(_selectionResizeMode));
                return;
            }

            SetVisibleCursor(ViewerCursors.Arrow);
            return;
        }

        SelectionResizeMode resizeMode = GetSelectionResizeMode(position);
        if (resizeMode != SelectionResizeMode.None)
        {
            SetVisibleCursor(GetSelectionResizeCursor(resizeMode));
            return;
        }

        Rect rect = NormalizeSelectionRect(_selectionRect);
        SetVisibleCursor(rect.Contains(position)
            ? ViewerCursors.Move
            : ViewerCursors.Arrow);
    }

    private static Cursor GetSelectionResizeCursor(SelectionResizeMode resizeMode)
    {
        return resizeMode switch
        {
            SelectionResizeMode.Left or SelectionResizeMode.Right =>
                ViewerCursors.HorizontalResize,
            SelectionResizeMode.Top or SelectionResizeMode.Bottom =>
                ViewerCursors.VerticalResize,
            SelectionResizeMode.TopLeft or SelectionResizeMode.BottomRight =>
                ViewerCursors.TopLeftResize,
            SelectionResizeMode.TopRight or SelectionResizeMode.BottomLeft =>
                ViewerCursors.TopRightResize,
            _ => ViewerCursors.Arrow
        };
    }

    private void MoveSelection(Point position)
    {
        Vector delta = position - _pointerPressPosition;
        int deltaX = ScreenDeltaToPixelDelta(delta.X);
        int deltaY = ScreenDeltaToPixelDelta(delta.Y);
        int maxLeft = Math.Max(0, _bitmap?.PixelSize.Width - _selectionStartPixelRect.Width ?? 0);
        int maxTop = Math.Max(0, _bitmap?.PixelSize.Height - _selectionStartPixelRect.Height ?? 0);
        int left = Math.Clamp(_selectionStartPixelRect.X + deltaX, 0, maxLeft);
        int top = Math.Clamp(_selectionStartPixelRect.Y + deltaY, 0, maxTop);
        SetSelectionPixelRect(new PixelRect(
            left,
            top,
            _selectionStartPixelRect.Width,
            _selectionStartPixelRect.Height));
        UpdateSelectionOverlay();
    }

    private void ResizeSelection(Point position)
    {
        if (_bitmap is null)
        {
            return;
        }

        int left = _selectionStartPixelRect.X;
        int top = _selectionStartPixelRect.Y;
        int right = _selectionStartPixelRect.X + _selectionStartPixelRect.Width;
        int bottom = _selectionStartPixelRect.Y + _selectionStartPixelRect.Height;
        int minimumPixelSize = GetMinimumSelectionPixelSize();

        if (_selectionResizeMode.HasFlag(SelectionResizeMode.Left))
        {
            left = Math.Clamp(ScreenXToPixelBoundary(position.X), 0, right - minimumPixelSize);
        }

        if (_selectionResizeMode.HasFlag(SelectionResizeMode.Right))
        {
            right = Math.Clamp(ScreenXToPixelBoundary(position.X), left + minimumPixelSize, _bitmap.PixelSize.Width);
        }

        if (_selectionResizeMode.HasFlag(SelectionResizeMode.Top))
        {
            top = Math.Clamp(ScreenYToPixelBoundary(position.Y), 0, bottom - minimumPixelSize);
        }

        if (_selectionResizeMode.HasFlag(SelectionResizeMode.Bottom))
        {
            bottom = Math.Clamp(ScreenYToPixelBoundary(position.Y), top + minimumPixelSize, _bitmap.PixelSize.Height);
        }

        SetSelectionPixelRect(new PixelRect(left, top, right - left, bottom - top));
        UpdateSelectionOverlay();
    }

    private WriteableBitmap? CreateSelectedBitmapOrDefault()
    {
        PixelRect? sourceRect = GetNormalizedSelectionPixelRect();

        if ((_bitmap is null) || (sourceRect is not { } validSourceRect))
        {
            return null;
        }

        return BitmapPixelCopy.CreateCrop(_bitmap, validSourceRect);
    }

    private PixelRect? GetNormalizedSelectionPixelRect()
    {
        return _bitmap is null
            ? null
            : BitmapPixelCopy.NormalizeSourceRect(_bitmap.PixelSize, _selectionPixelRect);
    }

    private void ScheduleSelectionClipboardPreparation()
    {
        Bitmap? sourceBitmap = _bitmap;

        if (!_isFullResolutionImageReady || (sourceBitmap is null))
        {
            CancelSelectionClipboardPreparation();
            return;
        }

        PixelRect? sourceRect = GetNormalizedSelectionPixelRect();

        if (sourceRect is not { } validSourceRect)
        {
            CancelSelectionClipboardPreparation();
            return;
        }

        if ((_selectionPreparationTask is not null)
            && (_selectionPreparationRect == validSourceRect))
        {
            return;
        }

        CancelSelectionClipboardPreparation();
        RenderTargetBitmap bitmap = BitmapPixelCopy.CreateRenderedCrop(
            sourceBitmap,
            validSourceRect);
        CancellationTokenSource cancellation = new();
        _selectionPreparationCancellation = cancellation;
        _selectionPreparationRect = validSourceRect;
        _selectionPreparationTask = PrepareSelectionClipboardImageAsync(bitmap, cancellation);
    }

    private void CancelSelectionClipboardPreparation()
    {
        CancellationTokenSource? cancellation = _selectionPreparationCancellation;
        _selectionPreparationCancellation = null;
        _selectionPreparationTask = null;
        _selectionPreparationRect = new PixelRect();
        cancellation?.Cancel();
    }

    private async Task<PreparedClipboardImage?> GetPreparedSelectionClipboardImageAsync(
        CancellationToken ct)
    {
        PixelRect? sourceRect = GetNormalizedSelectionPixelRect();

        if (sourceRect is not { } validSourceRect)
        {
            return null;
        }

        if ((_selectionPreparationTask is null)
            || (_selectionPreparationRect != validSourceRect))
        {
            ScheduleSelectionClipboardPreparation();
        }

        Task<PreparedClipboardImage?>? preparationTask = _selectionPreparationTask;

        return preparationTask is null
            ? null
            : await preparationTask.WaitAsync(ct);
    }

    private async Task<PreparedClipboardImage?> PrepareSelectionClipboardImageAsync(
        Bitmap bitmap,
        CancellationTokenSource cancellation)
    {
        try
        {
            return await _clipboardImagePreparer.PrepareImageAsync(
                bitmap,
                cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to preload the selection for copying.");
            return null;
        }
        finally
        {
            bitmap.Dispose();

            if (ReferenceEquals(_selectionPreparationCancellation, cancellation))
            {
                _selectionPreparationCancellation = null;
            }

            cancellation.Dispose();
        }
    }

    private void SetSelectionFromScreenRect(Rect screenRect)
    {
        SetSelectionPixelRect(GetSelectionSourcePixelRect(screenRect));
    }

    private void SetSelectionPixelRect(PixelRect pixelRect)
    {
        _selectionPixelRect = pixelRect;
        _selectionRect = GetSelectionScreenRect(pixelRect);
    }

    private PixelRect GetSelectionSourcePixelRect(Rect screenRect)
    {
        if (_bitmap is null)
        {
            return new PixelRect();
        }

        Rect rect = NormalizeSelectionRect(screenRect);
        double imageWidth = GetImageDipWidth();
        double imageHeight = GetImageDipHeight();
        double sourcePixelWidth = imageWidth / _bitmap.PixelSize.Width;
        double sourcePixelHeight = imageHeight / _bitmap.PixelSize.Height;
        double edgeSnapX = GetSelectionEdgeSnapSize(sourcePixelWidth);
        double edgeSnapY = GetSelectionEdgeSnapSize(sourcePixelHeight);
        double x = ((rect.Left - _offsetX) / imageWidth) * _bitmap.PixelSize.Width;
        double y = ((rect.Top - _offsetY) / imageHeight) * _bitmap.PixelSize.Height;
        double width = (rect.Width / imageWidth) * _bitmap.PixelSize.Width;
        double height = (rect.Height / imageHeight) * _bitmap.PixelSize.Height;

        int left = Math.Clamp((int)Math.Floor(x), 0, _bitmap.PixelSize.Width - 1);
        int top = Math.Clamp((int)Math.Floor(y), 0, _bitmap.PixelSize.Height - 1);
        int right = Math.Clamp((int)Math.Ceiling(x + width), left + 1, _bitmap.PixelSize.Width);
        int bottom = Math.Clamp((int)Math.Ceiling(y + height), top + 1, _bitmap.PixelSize.Height);
        Rect imageRect = new(_offsetX, _offsetY, imageWidth, imageHeight);
        Size viewport = GetViewportSize();

        if (IsLeftImageEdgeVisible(imageRect, edgeSnapX)
            && (rect.Left <= imageRect.Left + edgeSnapX))
        {
            left = 0;
        }

        if (IsRightImageEdgeVisible(imageRect, viewport, edgeSnapX)
            && (rect.Right >= imageRect.Right - edgeSnapX))
        {
            right = _bitmap.PixelSize.Width;
        }

        if (IsTopImageEdgeVisible(imageRect, edgeSnapY)
            && (rect.Top <= imageRect.Top + edgeSnapY))
        {
            top = 0;
        }

        if (IsBottomImageEdgeVisible(imageRect, viewport, edgeSnapY)
            && (rect.Bottom >= imageRect.Bottom - edgeSnapY))
        {
            bottom = _bitmap.PixelSize.Height;
        }

        return new PixelRect(left, top, right - left, bottom - top);
    }

    private Rect GetSelectionScreenRect(PixelRect pixelRect)
    {
        if (_bitmap is null)
        {
            return new Rect();
        }

        double pixelWidth = GetImageDipWidth() / _bitmap.PixelSize.Width;
        double pixelHeight = GetImageDipHeight() / _bitmap.PixelSize.Height;
        double left = _offsetX + (pixelRect.X * pixelWidth);
        double top = _offsetY + (pixelRect.Y * pixelHeight);
        double width = pixelRect.Width * pixelWidth;
        double height = pixelRect.Height * pixelHeight;

        return new Rect(left, top, width, height);
    }

    private int ScreenDeltaToPixelDelta(double delta)
    {
        return (int)Math.Round(delta * RenderScaling / _scale);
    }

    private int ScreenXToPixelBoundary(double x)
    {
        if (_bitmap is null)
        {
            return 0;
        }

        double imageWidth = GetImageDipWidth();
        Rect imageRect = new(_offsetX, _offsetY, imageWidth, GetImageDipHeight());
        Size viewport = GetViewportSize();
        double edgeSnapSize = GetSelectionEdgeSnapSize(imageWidth / _bitmap.PixelSize.Width);

        if (IsLeftImageEdgeVisible(imageRect, edgeSnapSize)
            && (x <= imageRect.Left + edgeSnapSize))
        {
            return 0;
        }

        if (IsRightImageEdgeVisible(imageRect, viewport, edgeSnapSize)
            && (x >= imageRect.Right - edgeSnapSize))
        {
            return _bitmap.PixelSize.Width;
        }

        double pixel = ((x - _offsetX) / imageWidth) * _bitmap.PixelSize.Width;

        return Math.Clamp((int)Math.Round(pixel), 0, _bitmap.PixelSize.Width);
    }

    private int ScreenYToPixelBoundary(double y)
    {
        if (_bitmap is null)
        {
            return 0;
        }

        double imageHeight = GetImageDipHeight();
        Rect imageRect = new(_offsetX, _offsetY, GetImageDipWidth(), imageHeight);
        Size viewport = GetViewportSize();
        double edgeSnapSize = GetSelectionEdgeSnapSize(imageHeight / _bitmap.PixelSize.Height);

        if (IsTopImageEdgeVisible(imageRect, edgeSnapSize)
            && (y <= imageRect.Top + edgeSnapSize))
        {
            return 0;
        }

        if (IsBottomImageEdgeVisible(imageRect, viewport, edgeSnapSize)
            && (y >= imageRect.Bottom - edgeSnapSize))
        {
            return _bitmap.PixelSize.Height;
        }

        double pixel = ((y - _offsetY) / imageHeight) * _bitmap.PixelSize.Height;

        return Math.Clamp((int)Math.Round(pixel), 0, _bitmap.PixelSize.Height);
    }

    private double GetSelectionEdgeSnapSize(double sourcePixelScreenSize)
    {
        return Math.Max(sourcePixelScreenSize, 1d / RenderScaling);
    }

    private static bool IsLeftImageEdgeVisible(Rect imageRect, double edgeSnapSize)
    {
        return imageRect.Left >= -edgeSnapSize;
    }

    private static bool IsRightImageEdgeVisible(Rect imageRect, Size viewport, double edgeSnapSize)
    {
        return imageRect.Right <= viewport.Width + edgeSnapSize;
    }

    private static bool IsTopImageEdgeVisible(Rect imageRect, double edgeSnapSize)
    {
        return imageRect.Top >= -edgeSnapSize;
    }

    private static bool IsBottomImageEdgeVisible(Rect imageRect, Size viewport, double edgeSnapSize)
    {
        return imageRect.Bottom <= viewport.Height + edgeSnapSize;
    }

    private int GetMinimumSelectionPixelSize()
    {
        return Math.Max(1, (int)Math.Ceiling(MinimumSelectionSize * RenderScaling / _scale));
    }
}
