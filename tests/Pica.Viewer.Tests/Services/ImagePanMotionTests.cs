using Avalonia;
using FluentAssertions;
using Xunit;

using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.Services;

public sealed class ImagePanMotionTests
{
    private static readonly DateTimeOffset StartedAt = new(
        2026,
        7,
        15,
        12,
        0,
        0,
        TimeSpan.Zero);
    private static readonly Rect PanBounds = new(-100d, -100d, 100d, 100d);

    [Fact]
    public void Advance_WithSmoothMode_InterpolatesTowardTarget()
    {
        ImagePanMotion motion = CreateSmoothMotion();

        motion.Advance(
            TimeSpan.FromMilliseconds(16d),
            ImagePanMotionMode.Smooth,
            PanBounds);

        motion.CurrentOffset.X.Should().BeGreaterThan(-50d).And.BeLessThan(-30d);
        motion.CurrentOffset.Y.Should().Be(-50d);
    }

    [Fact]
    public void Advance_AfterReleaseWithInertia_ContinuesMovement()
    {
        ImagePanMotion motion = CreateInertiaMotion();
        motion.Release(
            ImagePanMotionMode.SmoothWithInertia,
            StartedAt.AddMilliseconds(32d));

        motion.Advance(
            TimeSpan.FromMilliseconds(16d),
            ImagePanMotionMode.SmoothWithInertia,
            PanBounds);

        motion.CurrentOffset.X.Should().BeGreaterThan(-80d);
        motion.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Release_AfterPointerStops_DiscardsInertiaWithoutSnapping()
    {
        ImagePanMotion motion = CreateInertiaMotion();

        motion.Release(
            ImagePanMotionMode.SmoothWithInertia,
            StartedAt.AddMilliseconds(100d));
        Point offsetAfterRelease = motion.CurrentOffset;
        bool isActiveAfterRelease = motion.IsActive;

        for (int i = 0; i < 60; i++)
        {
            motion.Advance(
                TimeSpan.FromMilliseconds(16d),
                ImagePanMotionMode.SmoothWithInertia,
                PanBounds);
        }

        offsetAfterRelease.Should().Be(new Point(-80d, -50d));
        isActiveAfterRelease.Should().BeTrue();
        motion.CurrentOffset.X.Should().BeApproximately(-60d, 0.001d);
        motion.CurrentOffset.Y.Should().Be(-50d);
        motion.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Advance_WithSmoothMode_UsesFastTripleTiming()
    {
        ImagePanMotion motion = CreateSmoothMotion();

        motion.Advance(
            TimeSpan.FromMilliseconds(10d),
            ImagePanMotionMode.Smooth,
            PanBounds);

        motion.CurrentOffset.X.Should().BeApproximately(-41.6549d, 0.001d);
        motion.CurrentOffset.Y.Should().Be(-50d);
    }

    private static ImagePanMotion CreateMotion(
        Point initialOffset,
        Vector movement)
    {
        ImagePanMotion motion = new();
        motion.Begin(initialOffset, StartedAt);
        motion.Move(
            movement,
            PanBounds,
            StartedAt.AddMilliseconds(16d));

        return motion;
    }

    private static ImagePanMotion CreateSmoothMotion()
    {
        return CreateMotion(
            new Point(-50d, -50d),
            new Vector(20d, 0d));
    }

    private static ImagePanMotion CreateInertiaMotion()
    {
        return CreateMotion(
            new Point(-80d, -50d),
            new Vector(20d, 0d));
    }
}
