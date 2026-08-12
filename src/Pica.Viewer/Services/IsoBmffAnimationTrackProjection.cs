using System.Buffers.Binary;

namespace Pica.Viewer.Services;

internal static class IsoBmffAnimationTrackProjection
{
    internal static byte[] Create(
        MemoryStream source,
        int selectedTrackIndex)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Length > int.MaxValue)
        {
            throw new InvalidDataException(
                "The ISO BMFF container is too large to project in memory.");
        }

        if (source.TryGetBuffer(
                out ArraySegment<byte> buffer)
            && (buffer.Array is byte[] data))
        {
            ReadOnlySpan<byte> sourceData = new(
                data,
                buffer.Offset,
                checked((int)source.Length));

            return Create(sourceData, selectedTrackIndex);
        }

        return Create(
            source.ToArray(),
            selectedTrackIndex);
    }

    internal static byte[] Create(
        ReadOnlySpan<byte> source,
        int selectedTrackIndex)
    {
        IReadOnlyList<IsoBmffAnimationTrack> tracks =
            IsoBmffAnimationMetadataReader.ReadAll(source);

        if (tracks.Count == 0)
        {
            throw new InvalidDataException(
                "The ISO BMFF container does not contain visual animation tracks.");
        }

        if ((selectedTrackIndex < 0)
            || (selectedTrackIndex >= tracks.Count))
        {
            throw new ArgumentOutOfRangeException(
                nameof(selectedTrackIndex),
                selectedTrackIndex,
                $"The selected animation track index must be between 0 and {tracks.Count - 1}.");
        }

        byte[] projectedData = source.ToArray();
        HashSet<int> hiddenTrackOffsets = [];
        HashSet<uint> hiddenTrackIds = [];

        for (int trackIndex = 0;
            trackIndex < tracks.Count;
            trackIndex++)
        {
            if (trackIndex == selectedTrackIndex)
            {
                continue;
            }

            IsoBmffAnimationTrack hiddenTrack =
                tracks[trackIndex];
            hiddenTrackOffsets.Add(
                hiddenTrack.BoxOffset);

            if (hiddenTrack.TrackId != 0)
            {
                hiddenTrackIds.Add(hiddenTrack.TrackId);
            }
        }

        if (!IsoBmffBoxReader.TryFind(
            source,
            0,
            source.Length,
            IsoBmffBoxTypes.Movie,
            out IsoBmffBox movieBox))
        {
            throw new InvalidDataException(
                "The ISO BMFF container does not contain a movie box.");
        }

        int offset = movieBox.PayloadStart;

        while (offset < movieBox.End)
        {
            if (!IsoBmffBoxReader.TryRead(
                source,
                offset,
                movieBox.End,
                out IsoBmffBox box))
            {
                throw new InvalidDataException(
                    "The ISO BMFF movie box contains an invalid child box.");
            }

            if ((box.Type == IsoBmffBoxTypes.Track)
                && (hiddenTrackOffsets.Contains(offset)
                    || IsoBmffAuxiliaryTrackReferenceReader
                        .ReferencesAny(
                            source,
                            box,
                            hiddenTrackIds)))
            {
                int typeOffset = checked(offset + 4);
                BinaryPrimitives.WriteUInt32BigEndian(
                    projectedData.AsSpan(typeOffset, 4),
                    IsoBmffBoxTypes.Free);
            }

            offset = box.End;
        }

        return projectedData;
    }
}
