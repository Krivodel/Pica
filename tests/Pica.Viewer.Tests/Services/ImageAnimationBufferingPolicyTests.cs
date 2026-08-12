using FluentAssertions;
using Xunit;

using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.Services;

public sealed class ImageAnimationBufferingPolicyTests
{
    [Theory]
    [InlineData(2, 2)]
    [InlineData(8, 8)]
    [InlineData(12, 12)]
    public void PlaybackStartFrameCount_WithConfiguredPolicy_ReturnsConfiguredThreshold(
        int configuredFrameCount,
        int expectedFrameCount)
    {
        ImageAnimationBufferingPolicy policy =
            GetPolicy(configuredFrameCount);

        policy.PlaybackStartFrameCount.Should().Be(
            expectedFrameCount);
    }

    [Theory]
    [InlineData(5, 5)]
    [InlineData(12, 12)]
    [InlineData(20, 12)]
    public void GetRequiredFrameCount_WithAnimation_ReturnsConfiguredThresholdCappedByTotal(
        int frameCount,
        int expectedFrameCount)
    {
        ImageAnimationBufferingPolicy policy =
            ImageAnimationBufferingPolicy.HighEfficiency;

        int requiredFrameCount =
            policy.GetRequiredFrameCount(frameCount);

        requiredFrameCount.Should().Be(
            expectedFrameCount);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(20, 2)]
    public void GetInitialSynchronousFrameCount_WithAnimation_ReturnsTwoCappedByTotal(
        int frameCount,
        int expectedFrameCount)
    {
        ImageAnimationBufferingPolicy policy =
            ImageAnimationBufferingPolicy.WebP;

        int initialFrameCount =
            policy.GetInitialSynchronousFrameCount(
                frameCount);

        initialFrameCount.Should().Be(
            expectedFrameCount);
    }

    private static ImageAnimationBufferingPolicy GetPolicy(
        int configuredFrameCount)
    {
        return configuredFrameCount switch
        {
            2 => ImageAnimationBufferingPolicy.Lightweight,
            8 => ImageAnimationBufferingPolicy.WebP,
            12 => ImageAnimationBufferingPolicy.HighEfficiency,
            _ => throw new ArgumentOutOfRangeException(
                nameof(configuredFrameCount),
                configuredFrameCount,
                "The configured frame count is not supported by the test.")
        };
    }
}
