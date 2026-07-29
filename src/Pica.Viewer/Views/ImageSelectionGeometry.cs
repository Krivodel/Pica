using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;

using Pica.Viewer.Resources;

namespace Pica.Viewer.Views;

internal sealed class ImageSelectionGeometry
{
    private const double SelectionToolbarGap = 10d;
    private const double SelectionHandleSize = 8d;
    private const double MinimumSelectionSize = 12d;

    private Bitmap? CurrentBitmap => _viewport.CurrentBitmap;
    private double RenderScaling => _topLevel.RenderScaling;

    private readonly TopLevel _topLevel;
    private readonly ImageViewportController _viewport;

    internal ImageSelectionGeometry(
        TopLevel topLevel,
        ImageViewportController viewport)
    {
        _topLevel = topLevel
            ?? throw new ArgumentNullException(nameof(topLevel));
        _viewport = viewport
            ?? throw new ArgumentNullException(nameof(viewport));
    }

    internal static Rect NormalizeRect(Rect rect)
    {
        double left = Math.Min(rect.Left, rect.Right);
        double top = Math.Min(rect.Top, rect.Bottom);
        double right = Math.Max(rect.Left, rect.Right);
        double bottom = Math.Max(rect.Top, rect.Bottom);

        return new Rect(
            left,
            top,
            right - left,
            bottom - top);
    }

    internal static Point GetToolbarPosition(
        Rect rect,
        Size viewport,
        double toolbarWidth)
    {
        double maximumToolbarX = Math.Max(
            0d,
            viewport.Width - toolbarWidth);
        double maximumToolbarY = Math.Max(
            0d,
            viewport.Height
                - ImageViewerVisualMetrics.SelectionToolbarHeight);
        double centeredX = Math.Clamp(
            rect.Left
                + ((rect.Width - toolbarWidth) / 2d),
            0d,
            maximumToolbarX);

        if (rect.Bottom
            + SelectionToolbarGap
            + ImageViewerVisualMetrics.SelectionToolbarHeight
            <= viewport.Height)
        {
            double toolbarY = Math.Clamp(
                rect.Bottom + SelectionToolbarGap,
                0d,
                maximumToolbarY);

            return new Point(centeredX, toolbarY);
        }

        double centeredY = Math.Clamp(
            rect.Top
                + ((rect.Height
                    - ImageViewerVisualMetrics.SelectionToolbarHeight)
                    / 2d),
            0d,
            maximumToolbarY);

        if (rect.Right
            + SelectionToolbarGap
            + toolbarWidth
            <= viewport.Width)
        {
            return new Point(
                rect.Right + SelectionToolbarGap,
                centeredY);
        }

        if (rect.Left
            - SelectionToolbarGap
            - toolbarWidth
            >= 0d)
        {
            return new Point(
                rect.Left - SelectionToolbarGap - toolbarWidth,
                centeredY);
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

    internal Point ClampPointToImage(Point position)
    {
        Rect imageRect = _viewport.GetVisibleImageRect();
        double x = Math.Clamp(
            position.X,
            imageRect.Left,
            imageRect.Right);
        double y = Math.Clamp(
            position.Y,
            imageRect.Top,
            imageRect.Bottom);

        return new Point(x, y);
    }

    internal SelectionResizeModes GetResizeMode(
        Rect selectionRect,
        Point position)
    {
        Rect rect = NormalizeRect(selectionRect);
        bool isLeftOfRect = position.X < rect.Left;
        bool isRightOfRect = position.X > rect.Right;
        bool isAboveRect = position.Y < rect.Top;
        bool isBelowRect = position.Y > rect.Bottom;
        SelectionResizeModes outsideResizeMode =
            GetResizeMode(
                isLeftOfRect,
                isRightOfRect,
                isAboveRect,
                isBelowRect);

        if (outsideResizeMode != SelectionResizeModes.None)
        {
            return outsideResizeMode;
        }

        bool nearLeft =
            Math.Abs(position.X - rect.Left)
            <= SelectionHandleSize;
        bool nearRight =
            Math.Abs(position.X - rect.Right)
            <= SelectionHandleSize;
        bool nearTop =
            Math.Abs(position.Y - rect.Top)
            <= SelectionHandleSize;
        bool nearBottom =
            Math.Abs(position.Y - rect.Bottom)
            <= SelectionHandleSize;

        return GetResizeMode(
            nearLeft,
            nearRight,
            nearTop,
            nearBottom);
    }

    internal PixelRect GetSourcePixelRect(Rect screenRect)
    {
        if (CurrentBitmap is null)
        {
            return new PixelRect();
        }

        Rect rect = NormalizeRect(screenRect);
        double imageWidth = _viewport.GetImageDipWidth();
        double imageHeight = _viewport.GetImageDipHeight();
        double sourcePixelWidth =
            imageWidth / CurrentBitmap.PixelSize.Width;
        double sourcePixelHeight =
            imageHeight / CurrentBitmap.PixelSize.Height;
        double edgeSnapX =
            GetEdgeSnapSize(sourcePixelWidth);
        double edgeSnapY =
            GetEdgeSnapSize(sourcePixelHeight);
        double x =
            ((rect.Left - _viewport.OffsetX) / imageWidth)
            * CurrentBitmap.PixelSize.Width;
        double y =
            ((rect.Top - _viewport.OffsetY) / imageHeight)
            * CurrentBitmap.PixelSize.Height;
        double width =
            (rect.Width / imageWidth)
            * CurrentBitmap.PixelSize.Width;
        double height =
            (rect.Height / imageHeight)
            * CurrentBitmap.PixelSize.Height;
        int left = Math.Clamp(
            (int)Math.Floor(x),
            0,
            CurrentBitmap.PixelSize.Width - 1);
        int top = Math.Clamp(
            (int)Math.Floor(y),
            0,
            CurrentBitmap.PixelSize.Height - 1);
        int right = Math.Clamp(
            (int)Math.Ceiling(x + width),
            left + 1,
            CurrentBitmap.PixelSize.Width);
        int bottom = Math.Clamp(
            (int)Math.Ceiling(y + height),
            top + 1,
            CurrentBitmap.PixelSize.Height);
        Rect imageRect = new(
            _viewport.OffsetX,
            _viewport.OffsetY,
            imageWidth,
            imageHeight);
        Size viewport = _viewport.GetViewportSize();

        if (IsLeftImageEdgeVisible(
            imageRect,
            edgeSnapX)
            && (rect.Left
                <= imageRect.Left + edgeSnapX))
        {
            left = 0;
        }

        if (IsRightImageEdgeVisible(
            imageRect,
            viewport,
            edgeSnapX)
            && (rect.Right
                >= imageRect.Right - edgeSnapX))
        {
            right = CurrentBitmap.PixelSize.Width;
        }

        if (IsTopImageEdgeVisible(
            imageRect,
            edgeSnapY)
            && (rect.Top
                <= imageRect.Top + edgeSnapY))
        {
            top = 0;
        }

        if (IsBottomImageEdgeVisible(
            imageRect,
            viewport,
            edgeSnapY)
            && (rect.Bottom
                >= imageRect.Bottom - edgeSnapY))
        {
            bottom = CurrentBitmap.PixelSize.Height;
        }

        return new PixelRect(
            left,
            top,
            right - left,
            bottom - top);
    }

    internal Rect GetScreenRect(PixelRect pixelRect)
    {
        if (CurrentBitmap is null)
        {
            return new Rect();
        }

        double pixelWidth = _viewport.GetImageDipWidth()
            / CurrentBitmap.PixelSize.Width;
        double pixelHeight = _viewport.GetImageDipHeight()
            / CurrentBitmap.PixelSize.Height;
        double left = _viewport.OffsetX
            + (pixelRect.X * pixelWidth);
        double top = _viewport.OffsetY
            + (pixelRect.Y * pixelHeight);
        double width = pixelRect.Width * pixelWidth;
        double height = pixelRect.Height * pixelHeight;

        return new Rect(left, top, width, height);
    }

    internal int ScreenDeltaToPixelDelta(double delta)
    {
        return (int)Math.Round(
            delta * RenderScaling / _viewport.Scale);
    }

    internal int ScreenXToPixelBoundary(double x)
    {
        if (CurrentBitmap is null)
        {
            return 0;
        }

        double imageWidth = _viewport.GetImageDipWidth();
        Rect imageRect = new(
            _viewport.OffsetX,
            _viewport.OffsetY,
            imageWidth,
            _viewport.GetImageDipHeight());
        Size viewport = _viewport.GetViewportSize();
        double edgeSnapSize = GetEdgeSnapSize(
            imageWidth / CurrentBitmap.PixelSize.Width);

        if (IsLeftImageEdgeVisible(
            imageRect,
            edgeSnapSize)
            && (x <= imageRect.Left + edgeSnapSize))
        {
            return 0;
        }

        if (IsRightImageEdgeVisible(
            imageRect,
            viewport,
            edgeSnapSize)
            && (x >= imageRect.Right - edgeSnapSize))
        {
            return CurrentBitmap.PixelSize.Width;
        }

        double pixel =
            ((x - _viewport.OffsetX) / imageWidth)
            * CurrentBitmap.PixelSize.Width;

        return Math.Clamp(
            (int)Math.Round(pixel),
            0,
            CurrentBitmap.PixelSize.Width);
    }

    internal int ScreenYToPixelBoundary(double y)
    {
        if (CurrentBitmap is null)
        {
            return 0;
        }

        double imageHeight = _viewport.GetImageDipHeight();
        Rect imageRect = new(
            _viewport.OffsetX,
            _viewport.OffsetY,
            _viewport.GetImageDipWidth(),
            imageHeight);
        Size viewport = _viewport.GetViewportSize();
        double edgeSnapSize = GetEdgeSnapSize(
            imageHeight / CurrentBitmap.PixelSize.Height);

        if (IsTopImageEdgeVisible(
            imageRect,
            edgeSnapSize)
            && (y <= imageRect.Top + edgeSnapSize))
        {
            return 0;
        }

        if (IsBottomImageEdgeVisible(
            imageRect,
            viewport,
            edgeSnapSize)
            && (y >= imageRect.Bottom - edgeSnapSize))
        {
            return CurrentBitmap.PixelSize.Height;
        }

        double pixel =
            ((y - _viewport.OffsetY) / imageHeight)
            * CurrentBitmap.PixelSize.Height;

        return Math.Clamp(
            (int)Math.Round(pixel),
            0,
            CurrentBitmap.PixelSize.Height);
    }

    internal int GetMinimumPixelSize()
    {
        return Math.Max(
            1,
            (int)Math.Ceiling(
                MinimumSelectionSize
                    * RenderScaling
                    / _viewport.Scale));
    }

    private static SelectionResizeModes GetResizeMode(
        bool isLeft,
        bool isRight,
        bool isTop,
        bool isBottom)
    {
        return (isLeft, isRight, isTop, isBottom) switch
        {
            (true, _, true, _) => SelectionResizeModes.TopLeft,
            (_, true, true, _) => SelectionResizeModes.TopRight,
            (_, true, _, true) => SelectionResizeModes.BottomRight,
            (true, _, _, true) => SelectionResizeModes.BottomLeft,
            (true, _, _, _) => SelectionResizeModes.Left,
            (_, true, _, _) => SelectionResizeModes.Right,
            (_, _, true, _) => SelectionResizeModes.Top,
            (_, _, _, true) => SelectionResizeModes.Bottom,
            _ => SelectionResizeModes.None
        };
    }

    private static bool IsLeftImageEdgeVisible(
        Rect imageRect,
        double edgeSnapSize)
    {
        return imageRect.Left >= -edgeSnapSize;
    }

    private static bool IsRightImageEdgeVisible(
        Rect imageRect,
        Size viewport,
        double edgeSnapSize)
    {
        return imageRect.Right
            <= viewport.Width + edgeSnapSize;
    }

    private static bool IsTopImageEdgeVisible(
        Rect imageRect,
        double edgeSnapSize)
    {
        return imageRect.Top >= -edgeSnapSize;
    }

    private static bool IsBottomImageEdgeVisible(
        Rect imageRect,
        Size viewport,
        double edgeSnapSize)
    {
        return imageRect.Bottom
            <= viewport.Height + edgeSnapSize;
    }

    private double GetEdgeSnapSize(
        double sourcePixelScreenSize)
    {
        return Math.Max(
            sourcePixelScreenSize,
            1d / RenderScaling);
    }
}
