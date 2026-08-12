using System.Buffers.Binary;

namespace Pica.Viewer.Services;

internal static class IsoBmffBoxReader
{
    internal static bool TryRead(
        ReadOnlySpan<byte> data,
        int offset,
        int parentEnd,
        out IsoBmffBox box)
    {
        box = default;

        if ((offset < 0)
            || (parentEnd > data.Length)
            || (offset > parentEnd - 8))
        {
            return false;
        }

        uint compactSize =
            BinaryPrimitives.ReadUInt32BigEndian(
                data.Slice(offset, 4));
        uint boxType =
            BinaryPrimitives.ReadUInt32BigEndian(
                data.Slice(offset + 4, 4));
        int headerSize = compactSize == 1
            ? 16
            : 8;
        ulong boxSize = compactSize switch
        {
            0 => checked((ulong)(parentEnd - offset)),
            1 when offset <= parentEnd - 16 =>
                BinaryPrimitives.ReadUInt64BigEndian(
                    data.Slice(offset + 8, 8)),
            _ => compactSize
        };

        if ((boxSize < checked((ulong)headerSize))
            || (boxSize
                > checked((ulong)(parentEnd - offset))))
        {
            return false;
        }

        box = new IsoBmffBox(
            boxType,
            offset + headerSize,
            checked(offset + (int)boxSize));

        return true;
    }

    internal static bool TryFind(
        ReadOnlySpan<byte> data,
        int start,
        int end,
        uint expectedType,
        out IsoBmffBox box)
    {
        box = default;
        int offset = start;

        while (offset < end)
        {
            if (!TryRead(
                data,
                offset,
                end,
                out IsoBmffBox currentBox))
            {
                return false;
            }

            if (currentBox.Type == expectedType)
            {
                box = currentBox;

                return true;
            }

            offset = currentBox.End;
        }

        return false;
    }

    internal static byte ReadByte(
        ReadOnlySpan<byte> data,
        int offset,
        int end)
    {
        return (offset >= 0)
            && (offset < end)
            && (offset < data.Length)
            ? data[offset]
            : (byte)0;
    }

    internal static uint ReadUInt32(
        ReadOnlySpan<byte> data,
        int offset,
        int end)
    {
        return (offset >= 0)
            && (offset <= end - 4)
            && (offset <= data.Length - 4)
            ? BinaryPrimitives.ReadUInt32BigEndian(
                data.Slice(offset, 4))
            : 0;
    }

    internal static ulong ReadUInt64(
        ReadOnlySpan<byte> data,
        int offset,
        int end)
    {
        return (offset >= 0)
            && (offset <= end - 8)
            && (offset <= data.Length - 8)
            ? BinaryPrimitives.ReadUInt64BigEndian(
                data.Slice(offset, 8))
            : 0;
    }
}
