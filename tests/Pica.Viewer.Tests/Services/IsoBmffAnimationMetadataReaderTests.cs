using FluentAssertions;
using Xunit;

using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.Services;

public sealed class IsoBmffAnimationMetadataReaderTests
{
    [Fact]
    public void Read_WithTimedAv1Sequence_ReturnsFrameTimingAndIterations()
    {
        byte[] content =
            IsoBmffAnimationTestData
                .CreateTimedAv1Sequence();
        using MemoryStream stream = new(content);

        IsoBmffAnimationMetadata? metadata =
            IsoBmffAnimationMetadataReader.Read(
                stream);

        metadata.Should().NotBeNull();
        metadata?.FrameDurations.Should().Equal(
            TimeSpan.FromMilliseconds(40d),
            TimeSpan.FromMilliseconds(40d),
            TimeSpan.FromMilliseconds(100d));
        metadata?.AnimationIterations.Should().Be(3);
    }

    [Fact]
    public void Read_WithoutMovieBox_ReturnsNull()
    {
        using MemoryStream stream = new(
            [1, 2, 3, 4]);

        IsoBmffAnimationMetadata? metadata =
            IsoBmffAnimationMetadataReader.Read(
                stream);

        metadata.Should().BeNull();
    }

    [Fact]
    public void ReadAll_WithThreeTimedSequences_ReturnsEveryVisualTrack()
    {
        byte[] content =
            IsoBmffAnimationTestData
                .CreateTimedAv1Sequences(3);

        IReadOnlyList<IsoBmffAnimationTrack> tracks =
            IsoBmffAnimationMetadataReader.ReadAll(
                content);

        tracks.Should().HaveCount(3);
        tracks.Select(track =>
                track.Metadata.FrameDurations.Count)
            .Should()
            .Equal(3, 3, 3);
        tracks.Select(track =>
                track.Metadata.AnimationIterations)
            .Should()
            .OnlyContain(iterations => iterations == 3);
    }

    [Theory]
    [InlineData("hvc1")]
    [InlineData("hev1")]
    public void ReadAll_WithHevcSequences_ReturnsEveryVisualTrack(
        string sampleEntryType)
    {
        byte[] content =
            IsoBmffAnimationTestData
                .CreateTimedHevcSequences(
                    2,
                    sampleEntryType);

        IReadOnlyList<IsoBmffAnimationTrack> tracks =
            IsoBmffAnimationMetadataReader.ReadAll(
                content);

        tracks.Should().HaveCount(2);
    }
}
