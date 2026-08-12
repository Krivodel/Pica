using System.Text;

using FluentAssertions;
using Xunit;

using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.Services;

public sealed class IsoBmffAnimationTrackProjectionTests
{
    [Fact]
    public void Create_WithSelectedTrack_HidesOtherVisualTracksWithoutChangingSource()
    {
        byte[] source = IsoBmffAnimationTestData
            .CreateTimedAv1Sequences(3);
        byte[] original = source.ToArray();
        IReadOnlyList<IsoBmffAnimationTrack> sourceTracks =
            IsoBmffAnimationMetadataReader.ReadAll(source);

        byte[] projection =
            IsoBmffAnimationTrackProjection.Create(
                source,
                1);

        source.Should().Equal(original);
        ReadBoxType(projection, sourceTracks[0].BoxOffset)
            .Should()
            .Be("free");
        ReadBoxType(projection, sourceTracks[1].BoxOffset)
            .Should()
            .Be("trak");
        ReadBoxType(projection, sourceTracks[2].BoxOffset)
            .Should()
            .Be("free");
        IsoBmffAnimationMetadataReader.ReadAll(projection)
            .Should()
            .ContainSingle();
    }

    [Fact]
    public void Create_WithMissingTrack_ThrowsArgumentOutOfRangeException()
    {
        byte[] source = IsoBmffAnimationTestData
            .CreateTimedAv1Sequence();

        Action act = () =>
            IsoBmffAnimationTrackProjection.Create(
                source,
                1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Create_WithAuxiliaryTrack_HidesAuxiliaryTrackOfHiddenAnimation()
    {
        byte[] source = IsoBmffAnimationTestData
            .CreateTimedAv1SequencesWithAuxiliaryTrack();
        IReadOnlyList<int> trackOffsets =
            ReadTrackOffsets(source);

        byte[] projection =
            IsoBmffAnimationTrackProjection.Create(
                source,
                0);

        trackOffsets.Should().HaveCount(3);
        ReadBoxType(projection, trackOffsets[0])
            .Should()
            .Be("trak");
        ReadBoxType(projection, trackOffsets[1])
            .Should()
            .Be("free");
        ReadBoxType(projection, trackOffsets[2])
            .Should()
            .Be("free");
    }

    private static string ReadBoxType(
        byte[] data,
        int boxOffset)
    {
        return Encoding.ASCII.GetString(
            data,
            boxOffset + 4,
            4);
    }

    private static IReadOnlyList<int> ReadTrackOffsets(
        byte[] data)
    {
        if (!IsoBmffBoxReader.TryFind(
            data,
            0,
            data.Length,
            IsoBmffBoxTypes.Movie,
            out IsoBmffBox movieBox))
        {
            throw new InvalidDataException(
                "The test container does not contain a movie box.");
        }

        List<int> trackOffsets = [];
        int offset = movieBox.PayloadStart;

        while (offset < movieBox.End)
        {
            if (!IsoBmffBoxReader.TryRead(
                data,
                offset,
                movieBox.End,
                out IsoBmffBox box))
            {
                throw new InvalidDataException(
                    "The test movie box contains an invalid child box.");
            }

            if (box.Type == IsoBmffBoxTypes.Track)
            {
                trackOffsets.Add(offset);
            }

            offset = box.End;
        }

        return trackOffsets.AsReadOnly();
    }
}
