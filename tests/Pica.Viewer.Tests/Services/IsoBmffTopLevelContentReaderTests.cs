using System.Buffers.Binary;

using FluentAssertions;
using Xunit;

using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.Services;

public sealed class IsoBmffTopLevelContentReaderTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Read_WithMetaAndMovie_ReturnsPhysicalOrder(
        bool metaFirst)
    {
        byte[] meta = CreateBox(IsoBmffBoxTypes.Meta);
        byte[] movie = CreateBox(IsoBmffBoxTypes.Movie);
        byte[] data = metaFirst
            ? [.. meta, .. movie]
            : [.. movie, .. meta];

        IsoBmffTopLevelContent? result =
            IsoBmffTopLevelContentReader.Read(data);
        IsoBmffTopLevelContent actual = result
            ?? throw new InvalidOperationException(
                "The mixed content descriptor was not read.");

        actual.StartsWithStillImages.Should().Be(metaFirst);
    }

    [Fact]
    public void Read_WithNestedMovie_DoesNotReportMixedContent()
    {
        byte[] nestedMovie = CreateBox(IsoBmffBoxTypes.Movie);
        byte[] meta = CreateBox(
            IsoBmffBoxTypes.Meta,
            nestedMovie);

        IsoBmffTopLevelContent? result =
            IsoBmffTopLevelContentReader.Read(meta);

        result.Should().BeNull();
    }

    private static byte[] CreateBox(
        uint type,
        ReadOnlySpan<byte> payload = default)
    {
        byte[] box = new byte[checked(8 + payload.Length)];
        BinaryPrimitives.WriteUInt32BigEndian(
            box.AsSpan(0, 4),
            checked((uint)box.Length));
        BinaryPrimitives.WriteUInt32BigEndian(
            box.AsSpan(4, 4),
            type);
        payload.CopyTo(box.AsSpan(8));

        return box;
    }
}
