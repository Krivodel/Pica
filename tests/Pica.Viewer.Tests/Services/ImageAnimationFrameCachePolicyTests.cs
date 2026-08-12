using Avalonia;
using FluentAssertions;
using Xunit;

using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.Services;

public sealed class ImageAnimationFrameCachePolicyTests
{
    [Fact]
    public void CanRetainAnimation_WhenDecodedFramesFitLimit_ReturnsTrue()
    {
        ImageAnimationFrameCachePolicy policy = new(64L);

        bool canRetain = policy.CanRetainAnimation(
            4,
            new PixelSize(2, 2));

        canRetain.Should().BeTrue();
    }

    [Fact]
    public void CanRetainAnimation_WhenDecodedFramesExceedLimit_ReturnsFalse()
    {
        ImageAnimationFrameCachePolicy policy = new(63L);

        bool canRetain = policy.CanRetainAnimation(
            4,
            new PixelSize(2, 2));

        canRetain.Should().BeFalse();
    }

    [Fact]
    public void CanRetainAnimation_WithFourKFiveHundredFrameAnimation_ReturnsFalse()
    {
        ImageAnimationFrameCachePolicy policy =
            ImageAnimationFrameCachePolicy.Default;

        bool canRetain = policy.CanRetainAnimation(
            500,
            new PixelSize(3840, 2160));

        canRetain.Should().BeFalse();
    }
}
