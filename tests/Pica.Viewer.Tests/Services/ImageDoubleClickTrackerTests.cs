using Avalonia;
using FluentAssertions;
using Xunit;

using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.Services;

public sealed class ImageDoubleClickTrackerTests
{
    private static readonly DateTimeOffset FirstClickAt =
        new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RegisterClick_WithTwoNearbyClicksInTime_ReturnsTrue()
    {
        ImageDoubleClickTracker tracker = new();
        Point firstPosition = new(100d, 100d);
        Point secondPosition = new(103d, 102d);
        tracker.RegisterClick(firstPosition, FirstClickAt);

        bool result = tracker.RegisterClick(
            secondPosition,
            FirstClickAt.AddMilliseconds(300d));

        result.Should().BeTrue();
    }

    [Fact]
    public void RegisterClick_WhenSecondClickIsLate_ReturnsFalse()
    {
        ImageDoubleClickTracker tracker = new();
        Point position = new(100d, 100d);
        tracker.RegisterClick(position, FirstClickAt);

        bool result = tracker.RegisterClick(
            position,
            FirstClickAt.AddMilliseconds(600d));

        result.Should().BeFalse();
    }

    [Fact]
    public void IsWithinMovementTolerance_WhenPointerWasDragged_ReturnsFalse()
    {
        ImageDoubleClickTracker tracker = new();
        Point start = new(100d, 100d);
        Point end = new(110d, 100d);

        bool result = tracker.IsWithinMovementTolerance(start, end);

        result.Should().BeFalse();
    }
}
