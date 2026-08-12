using System.Buffers.Binary;

namespace Pica.Viewer.Services;

internal static class IsoBmffSampleTableReader
{
    private const int MaximumFrameCount = 1_000_000;

    internal static bool TryRead(
        ReadOnlySpan<byte> data,
        IsoBmffBox mediaInformationBox,
        uint mediaTimescale,
        out uint sampleEntryType,
        out IReadOnlyList<TimeSpan>? frameDurations)
    {
        sampleEntryType = 0;
        frameDurations = null;

        if (!IsoBmffBoxReader.TryFind(
            data,
            mediaInformationBox.PayloadStart,
            mediaInformationBox.End,
            IsoBmffBoxTypes.SampleTable,
            out IsoBmffBox sampleTableBox))
        {
            return false;
        }

        int timeToSampleStart = 0;
        int timeToSampleEnd = 0;
        int expectedFrameCount = 0;
        int offset = sampleTableBox.PayloadStart;

        while (offset < sampleTableBox.End)
        {
            if (!IsoBmffBoxReader.TryRead(
                data,
                offset,
                sampleTableBox.End,
                out IsoBmffBox box))
            {
                return false;
            }

            if (box.Type
                == IsoBmffBoxTypes.SampleDescription)
            {
                sampleEntryType =
                    ReadSampleEntryType(
                        data,
                        box);
            }
            else if (box.Type
                == IsoBmffBoxTypes.SampleSize)
            {
                expectedFrameCount =
                    ReadSampleCount(
                        data,
                        box);
            }
            else if (box.Type
                == IsoBmffBoxTypes.TimeToSample)
            {
                timeToSampleStart = box.PayloadStart;
                timeToSampleEnd = box.End;
            }

            offset = box.End;
        }

        if ((expectedFrameCount <= 0)
            || (timeToSampleStart == 0))
        {
            return false;
        }

        List<TimeSpan>? durations =
            ReadFrameDurations(
                data,
                timeToSampleStart,
                timeToSampleEnd,
                mediaTimescale,
                expectedFrameCount);

        if (durations is null)
        {
            return false;
        }

        frameDurations = durations.AsReadOnly();

        return true;
    }

    private static List<TimeSpan>? ReadFrameDurations(
        ReadOnlySpan<byte> data,
        int payloadStart,
        int boxEnd,
        uint mediaTimescale,
        int expectedFrameCount)
    {
        uint entryCount = IsoBmffBoxReader.ReadUInt32(
            data,
            payloadStart + 4,
            boxEnd);
        int entryOffset = payloadStart + 8;
        List<TimeSpan> durations =
            new(expectedFrameCount);

        for (uint entryIndex = 0;
            entryIndex < entryCount;
            entryIndex++)
        {
            if ((entryOffset < 0)
                || (entryOffset > boxEnd - 8))
            {
                return null;
            }

            uint sampleCount =
                BinaryPrimitives.ReadUInt32BigEndian(
                    data.Slice(entryOffset, 4));
            uint sampleDelta =
                BinaryPrimitives.ReadUInt32BigEndian(
                    data.Slice(entryOffset + 4, 4));

            if ((sampleCount == 0)
                || (sampleDelta == 0)
                || (sampleCount
                    > expectedFrameCount - durations.Count))
            {
                return null;
            }

            double durationMilliseconds =
                sampleDelta
                * 1000d
                / mediaTimescale;
            TimeSpan duration =
                ImageAnimationTiming
                    .NormalizeFrameDuration(
                        durationMilliseconds);

            for (uint sampleIndex = 0;
                sampleIndex < sampleCount;
                sampleIndex++)
            {
                durations.Add(duration);
            }

            entryOffset += 8;
        }

        return durations.Count == expectedFrameCount
            ? durations
            : null;
    }

    private static uint ReadSampleEntryType(
        ReadOnlySpan<byte> data,
        IsoBmffBox sampleDescriptionBox)
    {
        uint entryCount = IsoBmffBoxReader.ReadUInt32(
            data,
            sampleDescriptionBox.PayloadStart + 4,
            sampleDescriptionBox.End);
        int firstEntryOffset =
            sampleDescriptionBox.PayloadStart + 8;

        if ((entryCount == 0)
            || !IsoBmffBoxReader.TryRead(
                data,
                firstEntryOffset,
                sampleDescriptionBox.End,
                out IsoBmffBox sampleEntryBox))
        {
            return 0;
        }

        return sampleEntryBox.Type;
    }

    private static int ReadSampleCount(
        ReadOnlySpan<byte> data,
        IsoBmffBox sampleSizeBox)
    {
        uint sampleCount = IsoBmffBoxReader.ReadUInt32(
            data,
            sampleSizeBox.PayloadStart + 8,
            sampleSizeBox.End);

        return (sampleCount == 0)
            || (sampleCount > MaximumFrameCount)
            ? 0
            : checked((int)sampleCount);
    }
}
