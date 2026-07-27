using Avalonia;

namespace Pica.Viewer.Services;

internal sealed class AspectRatioWindowResizeSession : WindowResizeSession
{
    private const int SizingDirectionLockThreshold = 3;

    private readonly int _frameWidth;
    private readonly int _frameHeight;
    private readonly double _aspectRatio;
    private WindowSizingBasis? _cornerSizingBasis;

    public AspectRatioWindowResizeSession(
        WindowRectangle initialRectangle,
        PixelPoint initialPointerPosition,
        WindowSizingEdges sizingEdges,
        int frameWidth,
        int frameHeight,
        double aspectRatio)
        : base(initialRectangle, initialPointerPosition, sizingEdges)
    {
        _frameWidth = frameWidth;
        _frameHeight = frameHeight;
        _aspectRatio = aspectRatio;
    }

    protected override WindowRectangle CalculateCore(int horizontalDelta, int verticalDelta)
    {
        WindowRectangle requestedRectangle = CreateRequestedRectangle(
            horizontalDelta,
            verticalDelta);
        WindowSizingBasis sizingBasis = GetSizingBasis(horizontalDelta, verticalDelta);

        return AspectRatioWindowSizing.Constrain(
            requestedRectangle,
            SizingEdges,
            sizingBasis,
            _frameWidth,
            _frameHeight,
            _aspectRatio);
    }

    private WindowRectangle CreateRequestedRectangle(
        int horizontalDelta,
        int verticalDelta)
    {
        WindowRectangle rectangle = InitialRectangle;

        if (SizingEdges.HasFlag(WindowSizingEdges.Left))
        {
            rectangle.Left += horizontalDelta;
        }
        else if (SizingEdges.HasFlag(WindowSizingEdges.Right))
        {
            rectangle.Right += horizontalDelta;
        }

        if (SizingEdges.HasFlag(WindowSizingEdges.Top))
        {
            rectangle.Top += verticalDelta;
        }
        else if (SizingEdges.HasFlag(WindowSizingEdges.Bottom))
        {
            rectangle.Bottom += verticalDelta;
        }

        return rectangle;
    }

    private WindowSizingBasis GetSizingBasis(int horizontalDelta, int verticalDelta)
    {
        bool includesHorizontalEdge = SizingEdges.IncludesHorizontalEdge();
        bool includesVerticalEdge = SizingEdges.IncludesVerticalEdge();

        if (includesHorizontalEdge && !includesVerticalEdge)
        {
            return WindowSizingBasis.Width;
        }

        if (includesVerticalEdge && !includesHorizontalEdge)
        {
            return WindowSizingBasis.Height;
        }

        if (_cornerSizingBasis is { } lockedBasis)
        {
            return lockedBasis;
        }

        int horizontalDistance = Math.Abs(horizontalDelta);
        int verticalDistance = Math.Abs(verticalDelta);

        if (Math.Max(horizontalDistance, verticalDistance) >= SizingDirectionLockThreshold)
        {
            _cornerSizingBasis = horizontalDistance >= verticalDistance
                ? WindowSizingBasis.Width
                : WindowSizingBasis.Height;
        }

        return _cornerSizingBasis ?? WindowSizingBasis.Width;
    }
}
