using FluentAssertions;
using Xunit;

using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.Services;

public sealed class ImageAnimationTimelineTests
{
    [Fact]
    public void GetFrameStartPosition_WithLastFrame_ReturnsElapsedDurationBeforeFrame()
    {
        ImageAnimationTimeline timeline = new(
            new List<TimeSpan>
            {
                TimeSpan.FromMilliseconds(50d),
                TimeSpan.FromMilliseconds(120d),
                TimeSpan.FromMilliseconds(30d)
            }.AsReadOnly());

        TimeSpan position = timeline.GetFrameStartPosition(2);

        position.Should().Be(TimeSpan.FromMilliseconds(170d));
        timeline.Duration.Should().Be(TimeSpan.FromMilliseconds(200d));
    }

    [Theory]
    [InlineData(0, 0d)]
    [InlineData(1, 50d)]
    [InlineData(2, 170d)]
    public void GetFrameStartPosition_WithVariableDurations_ReturnsElapsedPosition(
        int frameIndex,
        double expectedPositionMilliseconds)
    {
        ImageAnimationTimeline timeline = new(
            new List<TimeSpan>
            {
                TimeSpan.FromMilliseconds(50d),
                TimeSpan.FromMilliseconds(120d),
                TimeSpan.FromMilliseconds(30d)
            }.AsReadOnly());

        TimeSpan position = timeline.GetFrameStartPosition(
            frameIndex);

        position.Should().Be(
            TimeSpan.FromMilliseconds(
                expectedPositionMilliseconds));
    }

}
