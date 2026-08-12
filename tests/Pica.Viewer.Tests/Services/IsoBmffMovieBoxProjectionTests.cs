using System.Buffers.Binary;

using FluentAssertions;
using Xunit;

using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.Services;

public sealed class IsoBmffMovieBoxProjectionTests
{
    [Fact]
    public void Constructor_WithCompactMovieBox_ProjectsAndRestoresType()
    {
        byte[] source = CreateBox(
            IsoBmffBoxTypes.Movie,
            [1, 2, 3, 4]);
        byte[] original = source.ToArray();
        IsoBmffMovieBoxProjection projection = new(source);

        uint projectedType = ReadType(projection.Data, 0);
        projection.Dispose();

        projectedType.Should().Be(IsoBmffBoxTypes.Free);
        projection.Data.Should().Equal(original);
        source.Should().Equal(original);
    }

    [Fact]
    public void Constructor_WithExtendedMovieBox_ProjectsOnlyTypeField()
    {
        byte[] source = CreateExtendedBox(
            IsoBmffBoxTypes.Movie,
            [5, 6, 7, 8]);
        byte[] expected = source.ToArray();
        BinaryPrimitives.WriteUInt32BigEndian(
            expected.AsSpan(4, 4),
            IsoBmffBoxTypes.Free);

        using IsoBmffMovieBoxProjection projection = new(source);

        projection.Data.Should().Equal(expected);
    }

    [Fact]
    public void Constructor_WithMovieBoxExtendingToEnd_ProjectsType()
    {
        byte[] source = CreateBoxToEnd(
            IsoBmffBoxTypes.Movie,
            [9, 10, 11, 12]);

        using IsoBmffMovieBoxProjection projection = new(source);

        ReadType(projection.Data, 0)
            .Should()
            .Be(IsoBmffBoxTypes.Free);
    }

    [Fact]
    public void Constructor_WithNestedMovieBox_IgnoresNestedBox()
    {
        byte[] nestedMovie = CreateBox(
            IsoBmffBoxTypes.Movie,
            [13, 14]);
        byte[] source = CreateBox(
            IsoBmffBoxTypes.Meta,
            nestedMovie);

        using IsoBmffMovieBoxProjection projection = new(source);

        ReadType(projection.Data, 8)
            .Should()
            .Be(IsoBmffBoxTypes.Movie);
    }

    [Fact]
    public void Constructor_WithDamagedContainer_ThrowsWithoutChangingSource()
    {
        byte[] source = CreateBox(
            IsoBmffBoxTypes.Movie,
            [15, 16]);
        Array.Resize(ref source, source.Length + 4);
        byte[] original = source.ToArray();

        Action action = () =>
            new IsoBmffMovieBoxProjection(source);

        action.Should().Throw<InvalidDataException>();
        source.Should().Equal(original);
    }

    private static byte[] CreateBox(
        uint type,
        ReadOnlySpan<byte> payload)
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

    private static byte[] CreateExtendedBox(
        uint type,
        ReadOnlySpan<byte> payload)
    {
        byte[] box = new byte[checked(16 + payload.Length)];
        BinaryPrimitives.WriteUInt32BigEndian(
            box.AsSpan(0, 4),
            1);
        BinaryPrimitives.WriteUInt32BigEndian(
            box.AsSpan(4, 4),
            type);
        BinaryPrimitives.WriteUInt64BigEndian(
            box.AsSpan(8, 8),
            checked((ulong)box.Length));
        payload.CopyTo(box.AsSpan(16));

        return box;
    }

    private static byte[] CreateBoxToEnd(
        uint type,
        ReadOnlySpan<byte> payload)
    {
        byte[] box = CreateBox(type, payload);
        BinaryPrimitives.WriteUInt32BigEndian(
            box.AsSpan(0, 4),
            0);

        return box;
    }

    private static uint ReadType(byte[] data, int boxOffset)
    {
        return BinaryPrimitives.ReadUInt32BigEndian(
            data.AsSpan(boxOffset + 4, 4));
    }
}
