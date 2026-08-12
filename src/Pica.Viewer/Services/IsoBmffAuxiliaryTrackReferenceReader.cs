namespace Pica.Viewer.Services;

internal static class IsoBmffAuxiliaryTrackReferenceReader
{
    internal static bool ReferencesAny(
        ReadOnlySpan<byte> data,
        IsoBmffBox trackBox,
        IReadOnlySet<uint> trackIds)
    {
        if ((trackIds.Count == 0)
            || !IsoBmffBoxReader.TryFind(
                data,
                trackBox.PayloadStart,
                trackBox.End,
                IsoBmffBoxTypes.TrackReference,
                out IsoBmffBox trackReferenceBox))
        {
            return false;
        }

        int offset = trackReferenceBox.PayloadStart;

        while (offset < trackReferenceBox.End)
        {
            if (!IsoBmffBoxReader.TryRead(
                data,
                offset,
                trackReferenceBox.End,
                out IsoBmffBox referenceBox))
            {
                return false;
            }

            if ((referenceBox.Type
                    == IsoBmffBoxTypes.AuxiliaryReference)
                && ReferencesAnyTrackId(
                    data,
                    referenceBox,
                    trackIds))
            {
                return true;
            }

            offset = referenceBox.End;
        }

        return false;
    }

    private static bool ReferencesAnyTrackId(
        ReadOnlySpan<byte> data,
        IsoBmffBox referenceBox,
        IReadOnlySet<uint> trackIds)
    {
        int payloadLength = referenceBox.End
            - referenceBox.PayloadStart;

        if ((payloadLength == 0)
            || ((payloadLength % 4) != 0))
        {
            return false;
        }

        for (int offset = referenceBox.PayloadStart;
            offset < referenceBox.End;
            offset += 4)
        {
            uint trackId = IsoBmffBoxReader.ReadUInt32(
                data,
                offset,
                referenceBox.End);

            if (trackIds.Contains(trackId))
            {
                return true;
            }
        }

        return false;
    }
}
