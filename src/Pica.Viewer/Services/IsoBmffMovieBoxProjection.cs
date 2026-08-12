using System.Buffers.Binary;

namespace Pica.Viewer.Services;

internal sealed class IsoBmffMovieBoxProjection : IDisposable
{
    internal byte[] Data { get; }

    private readonly IReadOnlyList<int> _typeOffsets;
    private bool _disposed;

    internal IsoBmffMovieBoxProjection(ReadOnlySpan<byte> source)
    {
        Data = source.ToArray();
        List<int> typeOffsets = [];
        int offset = 0;

        while (offset < Data.Length)
        {
            if (!IsoBmffBoxReader.TryRead(
                Data,
                offset,
                Data.Length,
                out IsoBmffBox box))
            {
                throw new InvalidDataException(
                    "The ISO BMFF container contains an invalid top-level box.");
            }

            if (box.Type == IsoBmffBoxTypes.Movie)
            {
                int typeOffset = checked(offset + 4);
                BinaryPrimitives.WriteUInt32BigEndian(
                    Data.AsSpan(typeOffset, 4),
                    IsoBmffBoxTypes.Free);
                typeOffsets.Add(typeOffset);
            }

            offset = box.End;
        }

        _typeOffsets = typeOffsets.AsReadOnly();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (int typeOffset in _typeOffsets)
        {
            BinaryPrimitives.WriteUInt32BigEndian(
                Data.AsSpan(typeOffset, 4),
                IsoBmffBoxTypes.Movie);
        }
    }
}
