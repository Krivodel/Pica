namespace Pica.Viewer.Services;

internal static class IsoBmffAnimationMetadataReader
{
    internal static IsoBmffAnimationMetadata? Read(
        MemoryStream bufferedStream)
    {
        ArgumentNullException.ThrowIfNull(bufferedStream);

        IReadOnlyList<IsoBmffAnimationTrack> tracks =
            ReadAll(GetData(bufferedStream));

        return tracks.Count == 0
            ? null
            : tracks[0].Metadata;
    }

    internal static IReadOnlyList<IsoBmffAnimationTrack> ReadAll(
        MemoryStream bufferedStream)
    {
        ArgumentNullException.ThrowIfNull(bufferedStream);

        return ReadAll(GetData(bufferedStream));
    }

    internal static IReadOnlyList<IsoBmffAnimationTrack> ReadAll(
        ReadOnlySpan<byte> data)
    {
        List<IsoBmffAnimationTrack> tracks = [];

        if (!IsoBmffBoxReader.TryFind(
            data,
            0,
            data.Length,
            IsoBmffBoxTypes.Movie,
            out IsoBmffBox movieBox))
        {
            return tracks.AsReadOnly();
        }

        int offset = movieBox.PayloadStart;

        while (offset < movieBox.End)
        {
            if (!IsoBmffBoxReader.TryRead(
                data,
                offset,
                movieBox.End,
                out IsoBmffBox box))
            {
                return Array.Empty<IsoBmffAnimationTrack>();
            }

            if (box.Type == IsoBmffBoxTypes.Track)
            {
                IsoBmffAnimationMetadata? metadata =
                    IsoBmffAnimationTrackReader.Read(
                        data,
                        box,
                        out uint trackId);

                if (metadata is not null)
                {
                    tracks.Add(new IsoBmffAnimationTrack(
                        offset,
                        box.End,
                        trackId,
                        metadata));
                }
            }

            offset = box.End;
        }

        return tracks.AsReadOnly();
    }

    private static ReadOnlySpan<byte> GetData(
        MemoryStream bufferedStream)
    {
        if (bufferedStream.Length > int.MaxValue)
        {
            return ReadOnlySpan<byte>.Empty;
        }

        if (bufferedStream.TryGetBuffer(
                out ArraySegment<byte> buffer)
            && (buffer.Array is byte[] bufferedBytes))
        {
            return new ReadOnlySpan<byte>(
                bufferedBytes,
                buffer.Offset,
                checked((int)bufferedStream.Length));
        }

        return bufferedStream.ToArray();
    }
}
