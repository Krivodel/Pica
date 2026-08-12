using FluentAssertions;
using Xunit;

using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.Services;

public sealed class MagickAnimationDecodingPolicyTests
{
    [Fact]
    public void GetInitialDecodeFrameCount_WithLongSequence_UsesRunwayAndFrameFraction()
    {
        IReadOnlyList<TimeSpan> frameDurations =
            Enumerable.Repeat(
                    TimeSpan.FromMilliseconds(1000d / 30d),
                    325)
                .ToList()
                .AsReadOnly();

        int frameCount =
            MagickAnimationDecodingPolicy
                .DependentFrameSequence
                .GetInitialDecodeFrameCount(
                    frameDurations,
                    12);

        frameCount.Should().Be(130);
    }

    [Fact]
    public void GetInitialDecodeFrameCount_WithShortSequence_DecodesEntireSequence()
    {
        IReadOnlyList<TimeSpan> frameDurations =
            Enumerable.Repeat(
                    TimeSpan.FromMilliseconds(50d),
                    40)
                .ToList()
                .AsReadOnly();

        int frameCount =
            MagickAnimationDecodingPolicy
                .DependentFrameSequence
                .GetInitialDecodeFrameCount(
                    frameDurations,
                    12);

        frameCount.Should().Be(40);
    }
}
