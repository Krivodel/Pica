using System.Buffers.Binary;

using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.Services;

internal static class IsoBmffMultiAnimationTestData
{
    private const uint ChunkOffsetBoxType = 0x7374636F;
    private const uint DataInformationBoxType = 0x64696E66;
    private const uint ExtendedChunkOffsetBoxType = 0x636F3634;

    internal static byte[] DuplicateAnimationTracks(
        ReadOnlySpan<byte> source)
    {
        if (!TryFindMovieBox(
            source,
            out int movieOffset,
            out IsoBmffBox movieBox))
        {
            throw new InvalidDataException(
                "The test animation does not contain a movie box.");
        }

        List<byte[]> trackCopies = [];
        int childOffset = movieBox.PayloadStart;

        while (childOffset < movieBox.End)
        {
            if (!IsoBmffBoxReader.TryRead(
                source,
                childOffset,
                movieBox.End,
                out IsoBmffBox childBox))
            {
                throw new InvalidDataException(
                    "The test movie box contains an invalid child box.");
            }

            if (childBox.Type == IsoBmffBoxTypes.Track)
            {
                trackCopies.Add(source[
                    childOffset..childBox.End].ToArray());
            }

            childOffset = childBox.End;
        }

        if (trackCopies.Count == 0)
        {
            throw new InvalidDataException(
                "The test animation does not contain tracks.");
        }

        int addedLength = trackCopies.Sum(
            track => track.Length);
        byte[] output = new byte[
            checked(source.Length + addedLength)];
        source[..movieBox.End].CopyTo(output);
        int outputOffset = movieBox.End;

        foreach (byte[] trackCopy in trackCopies)
        {
            trackCopy.CopyTo(output, outputOffset);
            outputOffset = checked(
                outputOffset + trackCopy.Length);
        }

        source[movieBox.End..].CopyTo(
            output.AsSpan(outputOffset));
        int movieHeaderSize = movieBox.PayloadStart
            - movieOffset;
        int newMovieEnd = checked(
            movieBox.End + addedLength);
        WriteBoxSize(
            output,
            movieOffset,
            movieHeaderSize,
            newMovieEnd - movieOffset);
        PatchChunkOffsets(
            output,
            movieBox.PayloadStart,
            newMovieEnd,
            addedLength);

        return output;
    }

    private static bool IsContainerBox(uint boxType)
    {
        return (boxType == IsoBmffBoxTypes.Track)
            || (boxType == IsoBmffBoxTypes.Media)
            || (boxType == IsoBmffBoxTypes.MediaInformation)
            || (boxType == IsoBmffBoxTypes.SampleTable)
            || (boxType == IsoBmffBoxTypes.Edit)
            || (boxType == DataInformationBoxType);
    }

    private static void PatchChunkOffsets(
        byte[] data,
        int start,
        int end,
        int offsetDelta)
    {
        int offset = start;

        while (offset < end)
        {
            if (!IsoBmffBoxReader.TryRead(
                data,
                offset,
                end,
                out IsoBmffBox box))
            {
                throw new InvalidDataException(
                    "The test movie contains an invalid nested box.");
            }

            if (box.Type == ChunkOffsetBoxType)
            {
                PatchCompactChunkOffsets(
                    data,
                    box,
                    offsetDelta);
            }
            else if (box.Type == ExtendedChunkOffsetBoxType)
            {
                PatchExtendedChunkOffsets(
                    data,
                    box,
                    offsetDelta);
            }

            if (IsContainerBox(box.Type))
            {
                PatchChunkOffsets(
                    data,
                    box.PayloadStart,
                    box.End,
                    offsetDelta);
            }

            offset = box.End;
        }
    }

    private static void PatchCompactChunkOffsets(
        byte[] data,
        IsoBmffBox box,
        int offsetDelta)
    {
        uint entryCount = IsoBmffBoxReader.ReadUInt32(
            data,
            box.PayloadStart + 4,
            box.End);

        for (uint entryIndex = 0;
            entryIndex < entryCount;
            entryIndex++)
        {
            int entryOffset = checked(
                box.PayloadStart
                + 8
                + checked((int)entryIndex * 4));
            uint sourceOffset = IsoBmffBoxReader.ReadUInt32(
                data,
                entryOffset,
                box.End);
            BinaryPrimitives.WriteUInt32BigEndian(
                data.AsSpan(entryOffset, 4),
                checked(sourceOffset + (uint)offsetDelta));
        }
    }

    private static void PatchExtendedChunkOffsets(
        byte[] data,
        IsoBmffBox box,
        int offsetDelta)
    {
        uint entryCount = IsoBmffBoxReader.ReadUInt32(
            data,
            box.PayloadStart + 4,
            box.End);

        for (uint entryIndex = 0;
            entryIndex < entryCount;
            entryIndex++)
        {
            int entryOffset = checked(
                box.PayloadStart
                + 8
                + checked((int)entryIndex * 8));
            ulong sourceOffset = IsoBmffBoxReader.ReadUInt64(
                data,
                entryOffset,
                box.End);
            BinaryPrimitives.WriteUInt64BigEndian(
                data.AsSpan(entryOffset, 8),
                checked(sourceOffset + (ulong)offsetDelta));
        }
    }

    private static bool TryFindMovieBox(
        ReadOnlySpan<byte> source,
        out int movieOffset,
        out IsoBmffBox movieBox)
    {
        movieOffset = 0;
        movieBox = default;
        int offset = 0;

        while (offset < source.Length)
        {
            if (!IsoBmffBoxReader.TryRead(
                source,
                offset,
                source.Length,
                out IsoBmffBox box))
            {
                return false;
            }

            if (box.Type == IsoBmffBoxTypes.Movie)
            {
                movieOffset = offset;
                movieBox = box;

                return true;
            }

            offset = box.End;
        }

        return false;
    }

    private static void WriteBoxSize(
        byte[] data,
        int boxOffset,
        int headerSize,
        int boxSize)
    {
        if (headerSize == 8)
        {
            BinaryPrimitives.WriteUInt32BigEndian(
                data.AsSpan(boxOffset, 4),
                checked((uint)boxSize));

            return;
        }

        if (headerSize == 16)
        {
            BinaryPrimitives.WriteUInt64BigEndian(
                data.AsSpan(boxOffset + 8, 8),
                checked((ulong)boxSize));

            return;
        }

        throw new InvalidDataException(
            "The test movie box has an unsupported header size.");
    }
}
