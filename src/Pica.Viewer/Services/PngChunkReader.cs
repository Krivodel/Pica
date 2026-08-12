using System.Buffers.Binary;

namespace Pica.Viewer.Services;

internal sealed class PngChunkReader
{
    private const ulong PngSignature =
        0x89504E470D0A1A0A;
    private const int PngSignatureLength = 8;
    private const int ChunkHeaderLength = 8;
    private const int ChunkCrcLength = 4;

    private readonly Stream _sourceStream;

    internal PngChunkReader(Stream sourceStream)
    {
        _sourceStream = sourceStream
            ?? throw new ArgumentNullException(
                nameof(sourceStream));

        if (!sourceStream.CanSeek)
        {
            throw new ArgumentException(
                "The PNG source stream must support seeking.",
                nameof(sourceStream));
        }
    }

    internal void ReadSignature()
    {
        Span<byte> signature = stackalloc byte[
            PngSignatureLength];
        _sourceStream.ReadExactly(signature);

        if (BinaryPrimitives.ReadUInt64BigEndian(signature)
            != PngSignature)
        {
            throw new InvalidDataException(
                "The image does not contain a valid PNG signature.");
        }
    }

    internal uint ReadNextChunkTypeAndSkipData(
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Span<byte> chunkHeader = stackalloc byte[
            ChunkHeaderLength];
        _sourceStream.ReadExactly(chunkHeader);
        uint dataLength =
            BinaryPrimitives.ReadUInt32BigEndian(
                chunkHeader);
        uint chunkType =
            BinaryPrimitives.ReadUInt32BigEndian(
                chunkHeader[sizeof(uint)..]);
        SkipChunkData(dataLength);

        return chunkType;
    }

    internal PngChunk ReadChunk(
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        byte[] chunkHeader = new byte[
            ChunkHeaderLength];
        _sourceStream.ReadExactly(chunkHeader);
        uint dataLength =
            BinaryPrimitives.ReadUInt32BigEndian(
                chunkHeader);
        uint chunkType =
            BinaryPrimitives.ReadUInt32BigEndian(
                chunkHeader.AsSpan(sizeof(uint)));
        byte[] chunkData = ReadChunkData(
            dataLength);
        byte[] chunkCrc = new byte[
            ChunkCrcLength];
        _sourceStream.ReadExactly(chunkCrc);
        ValidateChunkCrc(
            chunkType,
            chunkData,
            chunkCrc);
        byte[] rawContent = CreateRawContent(
            chunkHeader,
            chunkData,
            chunkCrc);

        return new PngChunk(
            chunkType,
            chunkData,
            rawContent);
    }

    internal byte[] ReadRequiredChunk(
        uint expectedChunkType,
        int expectedDataLength,
        CancellationToken ct)
    {
        PngChunk chunk = ReadChunk(ct);

        if ((chunk.Type != expectedChunkType)
            || (chunk.Data.Length
                != expectedDataLength))
        {
            throw new InvalidDataException(
                "The PNG does not contain a valid image header chunk.");
        }

        return chunk.Data;
    }

    private byte[] ReadChunkData(
        uint dataLength)
    {
        if (dataLength > int.MaxValue)
        {
            throw new InvalidDataException(
                "The PNG chunk is too large.");
        }

        ValidateRemainingLength(dataLength);
        byte[] chunkData = new byte[
            checked((int)dataLength)];
        _sourceStream.ReadExactly(chunkData);

        return chunkData;
    }

    private void SkipChunkData(
        uint dataLength)
    {
        ValidateRemainingLength(dataLength);
        _sourceStream.Seek(
            checked(
                (long)dataLength
                + ChunkCrcLength),
            SeekOrigin.Current);
    }

    private void ValidateRemainingLength(
        uint dataLength)
    {
        long requiredLength = checked(
            (long)dataLength
            + ChunkCrcLength);

        if (requiredLength
            > _sourceStream.Length
                - _sourceStream.Position)
        {
            throw new InvalidDataException(
                "The PNG contains a truncated chunk.");
        }
    }

    private static void ValidateChunkCrc(
        uint chunkType,
        ReadOnlySpan<byte> chunkData,
        ReadOnlySpan<byte> chunkCrc)
    {
        uint expectedCrc =
            BinaryPrimitives.ReadUInt32BigEndian(
                chunkCrc);
        uint actualCrc = PngCrc32.Compute(
            chunkType,
            chunkData);

        if (actualCrc != expectedCrc)
        {
            throw new InvalidDataException(
                "The PNG contains a chunk with an invalid checksum.");
        }
    }

    private static byte[] CreateRawContent(
        ReadOnlySpan<byte> chunkHeader,
        ReadOnlySpan<byte> chunkData,
        ReadOnlySpan<byte> chunkCrc)
    {
        byte[] rawContent = new byte[
            checked(
                chunkHeader.Length
                + chunkData.Length
                + chunkCrc.Length)];
        chunkHeader.CopyTo(rawContent);
        chunkData.CopyTo(
            rawContent.AsSpan(chunkHeader.Length));
        chunkCrc.CopyTo(
            rawContent.AsSpan(
                chunkHeader.Length
                + chunkData.Length));

        return rawContent;
    }
}
