using System.Buffers.Binary;
using System.Text;

namespace Pica.Viewer.Tests.Services;

internal static class IsoBmffAnimationTestData
{
    internal static byte[] CreateTimedAv1Sequence()
    {
        byte[] trackHeaderPayload = new byte[24];
        WriteUInt32(
            trackHeaderPayload,
            12,
            1);
        WriteUInt32(
            trackHeaderPayload,
            20,
            900);
        byte[] editListPayload = new byte[12];
        WriteUInt32(
            editListPayload,
            0,
            1);
        WriteUInt32(
            editListPayload,
            4,
            1);
        WriteUInt32(
            editListPayload,
            8,
            300);
        byte[] mediaHeaderPayload = new byte[16];
        WriteUInt32(
            mediaHeaderPayload,
            12,
            1000);
        byte[] handlerPayload = new byte[12];
        WriteType(
            handlerPayload,
            8,
            "pict");
        byte[] sampleDescriptionPayload =
            CreateSampleDescriptionPayload();
        byte[] timeToSamplePayload =
            CreateTimeToSamplePayload();
        byte[] sampleSizePayload = new byte[12];
        WriteUInt32(
            sampleSizePayload,
            8,
            3);
        byte[] sampleTable = CreateBox(
            "stbl",
            CreateBox(
                "stsd",
                sampleDescriptionPayload),
            CreateBox(
                "stts",
                timeToSamplePayload),
            CreateBox(
                "stsz",
                sampleSizePayload));
        byte[] mediaInformation = CreateBox(
            "minf",
            sampleTable);
        byte[] media = CreateBox(
            "mdia",
            CreateBox(
                "mdhd",
                mediaHeaderPayload),
            CreateBox(
                "hdlr",
                handlerPayload),
            mediaInformation);
        byte[] edit = CreateBox(
            "edts",
            CreateBox(
                "elst",
                editListPayload));
        byte[] track = CreateBox(
            "trak",
            CreateBox(
                "tkhd",
                trackHeaderPayload),
            edit,
            media);

        return CreateBox(
            "moov",
            track);
    }

    internal static byte[] CreateTimedAv1Sequences(
        int trackCount)
    {
        if (trackCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(trackCount),
                trackCount,
                "The track count must be positive.");
        }

        byte[] sequence = CreateTimedAv1Sequence();
        byte[] track = sequence
            .AsSpan(8)
            .ToArray();
        byte[][] tracks = new byte[trackCount][];

        for (int trackIndex = 0;
            trackIndex < trackCount;
            trackIndex++)
        {
            byte[] trackCopy = track.ToArray();
            WriteUInt32(
                trackCopy,
                28,
                checked((uint)(trackIndex + 1)));
            tracks[trackIndex] = trackCopy;
        }

        return CreateBox("moov", tracks);
    }

    internal static byte[] CreateTimedHevcSequences(
        int trackCount,
        string sampleEntryType)
    {
        if (sampleEntryType is not ("hvc1" or "hev1"))
        {
            throw new ArgumentException(
                "The HEVC sample entry type must be 'hvc1' or 'hev1'.",
                nameof(sampleEntryType));
        }

        byte[] sequence = CreateTimedAv1Sequences(
            trackCount);
        ReplaceTypes(
            sequence,
            "av01",
            sampleEntryType);

        return sequence;
    }

    internal static byte[] CreateTimedAv1SequencesWithAuxiliaryTrack()
    {
        byte[] sequence = CreateTimedAv1Sequences(2);
        int trackSize = checked(
            (sequence.Length - 8) / 2);
        byte[] firstTrack = sequence
            .AsSpan(8, trackSize)
            .ToArray();
        byte[] secondTrack = sequence
            .AsSpan(8 + trackSize, trackSize)
            .ToArray();
        byte[] auxiliaryPayload = secondTrack
            .AsSpan(8)
            .ToArray();
        byte[] pictureHandler =
            Encoding.ASCII.GetBytes("pict");
        int handlerOffset = auxiliaryPayload
            .AsSpan()
            .IndexOf(pictureHandler);

        if (handlerOffset < 0)
        {
            throw new InvalidDataException(
                "The test track does not contain a picture handler.");
        }

        Encoding.ASCII.GetBytes("auxv").CopyTo(
            auxiliaryPayload,
            handlerOffset);
        byte[] auxiliaryReferencePayload = new byte[4];
        WriteUInt32(
            auxiliaryReferencePayload,
            0,
            2);
        byte[] auxiliaryTrack = CreateBox(
            "trak",
            auxiliaryPayload,
            CreateBox(
                "tref",
                CreateBox(
                    "auxl",
                    auxiliaryReferencePayload)));
        WriteUInt32(
            auxiliaryTrack,
            28,
            3);

        return CreateBox(
            "moov",
            firstTrack,
            secondTrack,
            auxiliaryTrack);
    }

    private static byte[] CreateSampleDescriptionPayload()
    {
        byte[] sampleEntry = CreateBox(
            "av01",
            Array.Empty<byte>());
        byte[] payload = new byte[8 + sampleEntry.Length];
        WriteUInt32(
            payload,
            4,
            1);
        sampleEntry.CopyTo(
            payload,
            8);

        return payload;
    }

    private static void ReplaceTypes(
        byte[] data,
        string sourceType,
        string targetType)
    {
        byte[] sourceBytes =
            Encoding.ASCII.GetBytes(sourceType);
        byte[] targetBytes =
            Encoding.ASCII.GetBytes(targetType);
        int searchOffset = 0;

        while (searchOffset <= data.Length - sourceBytes.Length)
        {
            int relativeOffset = data
                .AsSpan(searchOffset)
                .IndexOf(sourceBytes);

            if (relativeOffset < 0)
            {
                return;
            }

            int typeOffset = checked(
                searchOffset + relativeOffset);
            targetBytes.CopyTo(data, typeOffset);
            searchOffset = checked(
                typeOffset + sourceBytes.Length);
        }
    }

    private static byte[] CreateTimeToSamplePayload()
    {
        byte[] payload = new byte[24];
        WriteUInt32(
            payload,
            4,
            2);
        WriteUInt32(
            payload,
            8,
            2);
        WriteUInt32(
            payload,
            12,
            40);
        WriteUInt32(
            payload,
            16,
            1);
        WriteUInt32(
            payload,
            20,
            100);

        return payload;
    }

    private static byte[] CreateBox(
        string type,
        params byte[][] payloadParts)
    {
        int payloadLength = payloadParts.Sum(
            payload => payload.Length);
        byte[] box = new byte[8 + payloadLength];
        WriteUInt32(
            box,
            0,
            checked((uint)box.Length));
        WriteType(
            box,
            4,
            type);
        int offset = 8;

        foreach (byte[] payloadPart in payloadParts)
        {
            payloadPart.CopyTo(
                box,
                offset);
            offset += payloadPart.Length;
        }

        return box;
    }

    private static void WriteUInt32(
        byte[] destination,
        int offset,
        uint value)
    {
        BinaryPrimitives.WriteUInt32BigEndian(
            destination.AsSpan(offset, 4),
            value);
    }

    private static void WriteType(
        byte[] destination,
        int offset,
        string type)
    {
        byte[] typeBytes =
            Encoding.ASCII.GetBytes(type);
        typeBytes.CopyTo(
            destination,
            offset);
    }
}
