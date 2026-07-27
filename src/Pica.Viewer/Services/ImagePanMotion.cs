using Avalonia;

namespace Pica.Viewer.Services;

internal sealed class ImagePanMotion
{
    public Point CurrentOffset { get; private set; }
    public bool IsActive { get; private set; }

    private const double MinimumPointerIntervalSeconds = 1d / 240d;
    private const double MaximumPointerIntervalSeconds = 0.1d;
    private const double MaximumFrameIntervalSeconds = 0.05d;
    private const double VelocityBlend = 0.35d;
    private const double SmoothingResponsiveness = 18d;
    private const double InertiaFriction = 5.5d;
    private const double StopDistance = 0.1d;
    private const double StopVelocity = 8d;
    private const double FastTimeScale = 3d;
    private static readonly TimeSpan InertiaMovementLifetime = TimeSpan.FromMilliseconds(50d);

    private Point _targetOffset;
    private Vector _velocity;
    private DateTimeOffset? _lastPointerTimestamp;
    private DateTimeOffset? _lastMovementTimestamp;
    private bool _isDragging;

    public void Reset(Point offset)
    {
        CurrentOffset = offset;
        _targetOffset = offset;
        _velocity = new Vector();
        _lastPointerTimestamp = null;
        _lastMovementTimestamp = null;
        _isDragging = false;
        IsActive = false;
    }

    public void Begin(Point offset, DateTimeOffset timestamp)
    {
        Reset(offset);
        _lastPointerTimestamp = timestamp;
        _isDragging = true;
    }

    public void Move(
        Vector delta,
        Rect bounds,
        DateTimeOffset timestamp)
    {
        double pointerInterval = GetPointerIntervalSeconds(timestamp);
        bool hasMovement = (Math.Abs(delta.X) > double.Epsilon)
            || (Math.Abs(delta.Y) > double.Epsilon);

        if (!hasMovement)
        {
            _velocity = new Vector();
            return;
        }

        _lastMovementTimestamp = timestamp;
        Vector measuredVelocity = new(
            delta.X / pointerInterval,
            delta.Y / pointerInterval);
        _velocity = new Vector(
            (_velocity.X * (1d - VelocityBlend)) + (measuredVelocity.X * VelocityBlend),
            (_velocity.Y * (1d - VelocityBlend)) + (measuredVelocity.Y * VelocityBlend));
        SetTargetOffset(_targetOffset + delta, bounds);

        IsActive = GetDistance(CurrentOffset, _targetOffset) > StopDistance;
    }

    public void Release(ImagePanMotionMode mode, DateTimeOffset timestamp)
    {
        _lastPointerTimestamp = null;
        _isDragging = false;
        bool hasRecentMovement = _lastMovementTimestamp is { } lastMovementTimestamp
            && ((timestamp - lastMovementTimestamp) <= InertiaMovementLifetime);

        if ((mode != ImagePanMotionMode.SmoothWithInertia) || !hasRecentMovement)
        {
            _velocity = new Vector();
        }

        IsActive = (GetDistance(CurrentOffset, _targetOffset) > StopDistance)
            || (GetVelocityLength() > StopVelocity);
    }

    public void Advance(
        TimeSpan elapsed,
        ImagePanMotionMode mode,
        Rect bounds)
    {
        if (!IsActive)
        {
            return;
        }

        double elapsedSeconds = Math.Clamp(
            elapsed.TotalSeconds,
            0d,
            MaximumFrameIntervalSeconds)
            * FastTimeScale;

        if (!_isDragging && (mode == ImagePanMotionMode.SmoothWithInertia))
        {
            SetTargetOffset(
                _targetOffset + (_velocity * elapsedSeconds),
                bounds);
            double damping = Math.Exp(-InertiaFriction * elapsedSeconds);
            _velocity *= damping;
        }

        double interpolation = 1d - Math.Exp(-SmoothingResponsiveness * elapsedSeconds);
        Vector remainingDistance = _targetOffset - CurrentOffset;
        CurrentOffset = ImageWindowGeometry.ClampOffset(
            CurrentOffset + (remainingDistance * interpolation),
            bounds);
        bool hasRemainingDistance = GetDistance(CurrentOffset, _targetOffset) > StopDistance;
        bool hasRemainingVelocity = !_isDragging
            && (mode == ImagePanMotionMode.SmoothWithInertia)
            && (GetVelocityLength() > StopVelocity);

        if (hasRemainingDistance || hasRemainingVelocity)
        {
            IsActive = true;
            return;
        }

        CurrentOffset = _targetOffset;
        _velocity = new Vector();
        IsActive = false;
    }

    private static double GetDistance(Point first, Point second)
    {
        Vector distance = second - first;

        return Math.Sqrt((distance.X * distance.X) + (distance.Y * distance.Y));
    }

    private double GetPointerIntervalSeconds(DateTimeOffset timestamp)
    {
        DateTimeOffset? previousTimestamp = _lastPointerTimestamp;
        _lastPointerTimestamp = timestamp;

        return previousTimestamp is null
            ? MinimumPointerIntervalSeconds
            : Math.Clamp(
                (timestamp - previousTimestamp.Value).TotalSeconds,
                MinimumPointerIntervalSeconds,
                MaximumPointerIntervalSeconds);
    }

    private double GetVelocityLength()
    {
        return Math.Sqrt((_velocity.X * _velocity.X) + (_velocity.Y * _velocity.Y));
    }

    private void SetTargetOffset(Point targetOffset, Rect bounds)
    {
        Point clampedOffset = ImageWindowGeometry.ClampOffset(targetOffset, bounds);
        bool isHorizontalBoundaryHit = Math.Abs(clampedOffset.X - targetOffset.X) > double.Epsilon;
        bool isVerticalBoundaryHit = Math.Abs(clampedOffset.Y - targetOffset.Y) > double.Epsilon;
        _targetOffset = clampedOffset;

        if (isHorizontalBoundaryHit)
        {
            _velocity = new Vector(0d, _velocity.Y);
        }

        if (isVerticalBoundaryHit)
        {
            _velocity = new Vector(_velocity.X, 0d);
        }
    }
}
