using Avalonia;

namespace Pica.Viewer.Services;

internal sealed class ImageDoubleClickTracker
{
    private const double MaximumDistance = 5d;
    private const int MaximumDelayMilliseconds = 500;

    private DateTimeOffset? _lastClickAt;
    private Point _lastClickPosition;

    public bool IsWithinMovementTolerance(Point start, Point end)
    {
        Vector movement = end - start;
        double maximumDistanceSquared = MaximumDistance * MaximumDistance;

        return ((movement.X * movement.X) + (movement.Y * movement.Y)) <= maximumDistanceSquared;
    }

    public bool RegisterClick(Point position, DateTimeOffset clickedAt)
    {
        bool isWithinDelay = _lastClickAt is { } previousClickAt
            && ((clickedAt - previousClickAt).TotalMilliseconds <= MaximumDelayMilliseconds);
        bool isWithinDistance = IsWithinMovementTolerance(_lastClickPosition, position);

        if (isWithinDelay && isWithinDistance)
        {
            _lastClickAt = null;
            return true;
        }

        _lastClickAt = clickedAt;
        _lastClickPosition = position;

        return false;
    }

    public void Reset()
    {
        _lastClickAt = null;
        _lastClickPosition = default;
    }
}
