using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Pica.Viewer.Views;

internal sealed class ViewerFloatingMenuAnimator : IDisposable
{
    internal const int OpeningDurationMilliseconds = 200;
    internal const int WidthRevealDurationMilliseconds = 120;
    internal const int HeightRevealDurationMilliseconds = 180;
    internal const int OpacityRevealDurationMilliseconds = 60;
    internal const int ClosingDurationMilliseconds = 60;
    internal const double InitialWidthRatio = 0.5d;
    internal const double InitialHeightRatio = 0.3d;

    internal bool IsClosing => _isClosing;

    private readonly Border _menu;
    private readonly ViewerFrameAnimationRunner _animationRunner;
    private readonly double _visibleOpacity;
    private readonly double _hiddenOpacity;
    private Size _menuSize;
    private ViewerFloatingMenuRevealOrigin _origin;
    private long _animationId;
    private bool _isClosing;
    private bool _isDisposed;

    internal ViewerFloatingMenuAnimator(
        Border menu,
        ViewerFrameAnimationRunner animationRunner,
        double visibleOpacity,
        double hiddenOpacity)
    {
        _menu = menu ?? throw new ArgumentNullException(nameof(menu));
        _animationRunner = animationRunner
            ?? throw new ArgumentNullException(nameof(animationRunner));
        _visibleOpacity = visibleOpacity;
        _hiddenOpacity = hiddenOpacity;
    }

    public void Dispose()
    {
        _isDisposed = true;
        _animationId++;
        _menu.IsHitTestVisible = false;
        _menu.IsVisible = false;
        _menu.Clip = null;
        _menu.Opacity = _hiddenOpacity;
    }

    internal static ViewerFloatingMenuRevealOrigin ResolveOrigin(
        Point pointerPosition,
        Point menuPosition,
        Size menuSize)
    {
        bool fromRight = menuPosition.X + menuSize.Width
            <= pointerPosition.X;
        bool fromBottom = menuPosition.Y + menuSize.Height
            <= pointerPosition.Y;

        return CreateOrigin(fromRight, fromBottom);
    }

    internal static ViewerFloatingMenuRevealOrigin ResolveNearestOrigin(
        Point anchorPosition,
        Size anchorSize,
        Point menuPosition,
        Size menuSize)
    {
        Point anchorCenter = new(
            anchorPosition.X + (anchorSize.Width / 2d),
            anchorPosition.Y + (anchorSize.Height / 2d));
        bool fromRight = anchorCenter.X
            >= menuPosition.X + (menuSize.Width / 2d);
        bool fromBottom = anchorCenter.Y
            >= menuPosition.Y + (menuSize.Height / 2d);

        return CreateOrigin(fromRight, fromBottom);
    }

    internal static Rect CalculateRevealBounds(
        Size menuSize,
        double widthRatio,
        double heightRatio,
        ViewerFloatingMenuRevealOrigin origin)
    {
        double width = menuSize.Width * Math.Clamp(widthRatio, 0d, 1d);
        double height = menuSize.Height * Math.Clamp(heightRatio, 0d, 1d);
        bool fromRight = origin is ViewerFloatingMenuRevealOrigin.TopRight
            or ViewerFloatingMenuRevealOrigin.BottomRight;
        bool fromBottom = origin is ViewerFloatingMenuRevealOrigin.BottomLeft
            or ViewerFloatingMenuRevealOrigin.BottomRight;

        return new Rect(
            fromRight ? menuSize.Width - width : 0d,
            fromBottom ? menuSize.Height - height : 0d,
            width,
            height);
    }

    internal void Open(
        Point pointerPosition,
        Point menuPosition,
        Size menuSize)
    {
        Open(ResolveOrigin(pointerPosition, menuPosition, menuSize), menuSize);
    }

    internal void Open(
        ViewerFloatingMenuRevealOrigin origin,
        Size menuSize)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        _animationId++;
        _isClosing = false;
        _menuSize = menuSize;
        _origin = origin;
        _menu.IsHitTestVisible = true;
        ApplyOpeningProgress(0d);

        long animationId = _animationId;
        _animationRunner.Start(
            TimeSpan.FromMilliseconds(OpeningDurationMilliseconds),
            () => (animationId == _animationId) && !_isDisposed,
            ApplyOpeningProgress,
            completed: () => CompleteOpen(animationId));
    }

    internal void Close()
    {
        if (!_menu.IsVisible || _isClosing || _isDisposed)
        {
            return;
        }

        _isClosing = true;
        _menu.IsHitTestVisible = false;
        long animationId = ++_animationId;
        double startingOpacity = _menu.Opacity;

        _animationRunner.Start(
            TimeSpan.FromMilliseconds(ClosingDurationMilliseconds),
            () => (animationId == _animationId) && !_isDisposed,
            progress => _menu.Opacity = startingOpacity
                + ((_hiddenOpacity - startingOpacity) * EaseOutCirc(progress)),
            completed: () => CompleteClose(animationId));
    }

    private static double EaseOutCirc(double progress)
    {
        double normalizedProgress = Math.Clamp(progress, 0d, 1d);
        double distanceToEnd = 1d - normalizedProgress;

        return Math.Sqrt(1d - (distanceToEnd * distanceToEnd));
    }

    private static ViewerFloatingMenuRevealOrigin CreateOrigin(
        bool fromRight,
        bool fromBottom)
    {
        if (fromRight)
        {
            return fromBottom
                ? ViewerFloatingMenuRevealOrigin.BottomRight
                : ViewerFloatingMenuRevealOrigin.TopRight;
        }

        return fromBottom
            ? ViewerFloatingMenuRevealOrigin.BottomLeft
            : ViewerFloatingMenuRevealOrigin.TopLeft;
    }

    private static double InterpolateRevealRatio(
        double initialRatio,
        double elapsedMilliseconds,
        int durationMilliseconds)
    {
        double progress = Math.Clamp(
            elapsedMilliseconds / durationMilliseconds,
            0d,
            1d);

        return initialRatio
            + ((1d - initialRatio) * EaseOutCirc(progress));
    }

    private void ApplyOpeningProgress(double progress)
    {
        double elapsedMilliseconds = Math.Clamp(progress, 0d, 1d)
            * OpeningDurationMilliseconds;
        double widthRatio = InterpolateRevealRatio(
            InitialWidthRatio,
            elapsedMilliseconds,
            WidthRevealDurationMilliseconds);
        double heightRatio = InterpolateRevealRatio(
            InitialHeightRatio,
            elapsedMilliseconds,
            HeightRevealDurationMilliseconds);
        double opacityProgress = Math.Clamp(
            elapsedMilliseconds / OpacityRevealDurationMilliseconds,
            0d,
            1d);

        _menu.Clip = new RectangleGeometry(CalculateRevealBounds(
            _menuSize,
            widthRatio,
            heightRatio,
            _origin));
        _menu.Opacity = _hiddenOpacity
            + ((_visibleOpacity - _hiddenOpacity)
                * EaseOutCirc(opacityProgress));
    }

    private void CompleteOpen(long animationId)
    {
        if ((animationId != _animationId) || _isDisposed)
        {
            return;
        }

        _menu.Clip = null;
        _menu.Opacity = _visibleOpacity;
    }

    private void CompleteClose(long animationId)
    {
        if ((animationId != _animationId) || _isDisposed)
        {
            return;
        }

        _isClosing = false;
        _menu.IsVisible = false;
        _menu.Clip = null;
        _menu.Opacity = _hiddenOpacity;
    }
}
