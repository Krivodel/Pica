namespace Pica.Viewer.Services;

internal static class IsoBmffAnimationTrackReader
{
    private const uint Av1SampleEntryType = 0x61763031;
    private const uint Hevc1SampleEntryType = 0x68766331;
    private const uint Hev1SampleEntryType = 0x68657631;
    private const uint PictureHandlerType = 0x70696374;
    private const uint VideoHandlerType = 0x76696465;

    internal static IsoBmffAnimationMetadata? Read(
        ReadOnlySpan<byte> data,
        IsoBmffBox trackBox,
        out uint trackId)
    {
        trackId = 0;
        ulong trackDuration = 0;
        ulong repeatedSegmentDuration = 0;
        IsoBmffBox? mediaBox = null;
        bool isRepeating = false;
        int offset = trackBox.PayloadStart;

        while (offset < trackBox.End)
        {
            if (!IsoBmffBoxReader.TryRead(
                data,
                offset,
                trackBox.End,
                out IsoBmffBox box))
            {
                return null;
            }

            if (box.Type == IsoBmffBoxTypes.TrackHeader)
            {
                trackId = ReadTrackId(
                    data,
                    box);
                trackDuration = ReadTrackDuration(
                    data,
                    box);
            }
            else if (box.Type == IsoBmffBoxTypes.Edit)
            {
                ReadEditList(
                    data,
                    box,
                    out isRepeating,
                    out repeatedSegmentDuration);
            }
            else if (box.Type == IsoBmffBoxTypes.Media)
            {
                mediaBox = box;
            }

            offset = box.End;
        }

        if ((mediaBox is not IsoBmffBox sequenceMediaBox)
            || !TryReadMedia(
                data,
                sequenceMediaBox,
                out uint handlerType,
                out uint sampleEntryType,
                out IReadOnlyList<TimeSpan>? frameDurations)
            || !IsPrimaryImageSequenceHandler(
                handlerType)
            || !IsSupportedSampleEntry(
                sampleEntryType)
            || (frameDurations
                is not { Count: > 1 }
                    sequenceFrameDurations))
        {
            return null;
        }

        uint animationIterations =
            GetAnimationIterations(
                trackDuration,
                isRepeating,
                repeatedSegmentDuration);

        return new IsoBmffAnimationMetadata(
            sequenceFrameDurations,
            animationIterations);
    }

    private static bool TryReadMedia(
        ReadOnlySpan<byte> data,
        IsoBmffBox mediaBox,
        out uint handlerType,
        out uint sampleEntryType,
        out IReadOnlyList<TimeSpan>? frameDurations)
    {
        handlerType = 0;
        sampleEntryType = 0;
        frameDurations = null;
        uint mediaTimescale = 0;
        IsoBmffBox? mediaInformationBox = null;
        int offset = mediaBox.PayloadStart;

        while (offset < mediaBox.End)
        {
            if (!IsoBmffBoxReader.TryRead(
                data,
                offset,
                mediaBox.End,
                out IsoBmffBox box))
            {
                return false;
            }

            if (box.Type == IsoBmffBoxTypes.MediaHeader)
            {
                mediaTimescale = ReadMediaTimescale(
                    data,
                    box);
            }
            else if (box.Type == IsoBmffBoxTypes.Handler)
            {
                handlerType =
                    IsoBmffBoxReader.ReadUInt32(
                        data,
                        box.PayloadStart + 8,
                        box.End);
            }
            else if (box.Type
                == IsoBmffBoxTypes.MediaInformation)
            {
                mediaInformationBox = box;
            }

            offset = box.End;
        }

        return (mediaTimescale > 0)
            && (mediaInformationBox
                is IsoBmffBox sequenceMediaInformationBox)
            && IsoBmffSampleTableReader.TryRead(
                data,
                sequenceMediaInformationBox,
                mediaTimescale,
                out sampleEntryType,
                out frameDurations);
    }

    private static ulong ReadTrackDuration(
        ReadOnlySpan<byte> data,
        IsoBmffBox trackHeaderBox)
    {
        byte version = IsoBmffBoxReader.ReadByte(
            data,
            trackHeaderBox.PayloadStart,
            trackHeaderBox.End);

        if (version > 1)
        {
            return 0;
        }

        int durationOffset = version == 1
            ? trackHeaderBox.PayloadStart + 28
            : trackHeaderBox.PayloadStart + 20;

        return version == 1
            ? IsoBmffBoxReader.ReadUInt64(
                data,
                durationOffset,
                trackHeaderBox.End)
            : IsoBmffBoxReader.ReadUInt32(
                data,
                durationOffset,
                trackHeaderBox.End);
    }

    private static uint ReadTrackId(
        ReadOnlySpan<byte> data,
        IsoBmffBox trackHeaderBox)
    {
        byte version = IsoBmffBoxReader.ReadByte(
            data,
            trackHeaderBox.PayloadStart,
            trackHeaderBox.End);

        if (version > 1)
        {
            return 0;
        }

        int trackIdOffset = version == 1
            ? trackHeaderBox.PayloadStart + 20
            : trackHeaderBox.PayloadStart + 12;

        return IsoBmffBoxReader.ReadUInt32(
            data,
            trackIdOffset,
            trackHeaderBox.End);
    }

    private static uint ReadMediaTimescale(
        ReadOnlySpan<byte> data,
        IsoBmffBox mediaHeaderBox)
    {
        byte version = IsoBmffBoxReader.ReadByte(
            data,
            mediaHeaderBox.PayloadStart,
            mediaHeaderBox.End);

        if (version > 1)
        {
            return 0;
        }

        int timescaleOffset = version == 1
            ? mediaHeaderBox.PayloadStart + 20
            : mediaHeaderBox.PayloadStart + 12;

        return IsoBmffBoxReader.ReadUInt32(
            data,
            timescaleOffset,
            mediaHeaderBox.End);
    }

    private static void ReadEditList(
        ReadOnlySpan<byte> data,
        IsoBmffBox editBox,
        out bool isRepeating,
        out ulong segmentDuration)
    {
        isRepeating = false;
        segmentDuration = 0;

        if (!IsoBmffBoxReader.TryFind(
            data,
            editBox.PayloadStart,
            editBox.End,
            IsoBmffBoxTypes.EditList,
            out IsoBmffBox editListBox))
        {
            return;
        }

        uint versionAndFlags =
            IsoBmffBoxReader.ReadUInt32(
                data,
                editListBox.PayloadStart,
                editListBox.End);
        byte version = checked(
            (byte)(versionAndFlags >> 24));
        uint flags = versionAndFlags
            & 0x00FFFFFF;
        uint entryCount =
            IsoBmffBoxReader.ReadUInt32(
                data,
                editListBox.PayloadStart + 4,
                editListBox.End);

        if ((version > 1)
            || ((flags & 1) == 0)
            || (entryCount != 1))
        {
            return;
        }

        isRepeating = true;
        segmentDuration = version == 1
            ? IsoBmffBoxReader.ReadUInt64(
                data,
                editListBox.PayloadStart + 8,
                editListBox.End)
            : IsoBmffBoxReader.ReadUInt32(
                data,
                editListBox.PayloadStart + 8,
                editListBox.End);
    }

    private static uint GetAnimationIterations(
        ulong trackDuration,
        bool isRepeating,
        ulong segmentDuration)
    {
        if (!isRepeating
            || (segmentDuration == 0)
            || (trackDuration == 0)
            || (trackDuration == uint.MaxValue)
            || (trackDuration == ulong.MaxValue))
        {
            return 0;
        }

        ulong iterationCount =
            (trackDuration / segmentDuration)
            + ((trackDuration % segmentDuration) == 0
                ? 0UL
                : 1UL);

        return iterationCount > uint.MaxValue
            ? 0
            : checked((uint)iterationCount);
    }

    private static bool IsPrimaryImageSequenceHandler(
        uint handlerType)
    {
        return (handlerType == PictureHandlerType)
            || (handlerType == VideoHandlerType);
    }

    private static bool IsSupportedSampleEntry(
        uint sampleEntryType)
    {
        return (sampleEntryType
                == Av1SampleEntryType)
            || (sampleEntryType
                == Hevc1SampleEntryType)
            || (sampleEntryType
                == Hev1SampleEntryType);
    }
}
