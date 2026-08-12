using System.Buffers.Binary;

namespace Pica.Viewer.Services;

internal static class ApngFramePngBuilder
{
    private const uint ImageHeaderChunkType =
        0x49484452;
    private const uint ImageDataChunkType =
        0x49444154;
    private const uint ImageEndChunkType =
        0x49454E44;
    private const int ChunkHeaderLength = 8;
    private const int ChunkCrcLength = 4;

    private static readonly byte[] PngSignature =
    [
        0x89,
        0x50,
        0x4E,
        0x47,
        0x0D,
        0x0A,
        0x1A,
        0x0A
    ];

    internal static byte[] Build(
        ApngAnimationData animation,
        ApngFrameData frame)
    {
        ArgumentNullException.ThrowIfNull(animation);
        ArgumentNullException.ThrowIfNull(frame);
        byte[] imageHeaderData =
            (byte[])animation.ImageHeaderData.Clone();
        BinaryPrimitives.WriteUInt32BigEndian(
            imageHeaderData,
            checked((uint)frame.Width));
        BinaryPrimitives.WriteUInt32BigEndian(
            imageHeaderData.AsSpan(sizeof(uint)),
            checked((uint)frame.Height));
        using MemoryStream output = new();
        output.Write(PngSignature);
        WriteChunk(
            output,
            ImageHeaderChunkType,
            imageHeaderData);

        foreach (byte[] sharedChunk in animation.SharedChunks)
        {
            output.Write(sharedChunk);
        }

        foreach (byte[] compressedData in frame.CompressedData)
        {
            WriteChunk(
                output,
                ImageDataChunkType,
                compressedData);
        }

        WriteChunk(
            output,
            ImageEndChunkType,
            ReadOnlySpan<byte>.Empty);

        return output.ToArray();
    }

    private static void WriteChunk(
        Stream output,
        uint chunkType,
        ReadOnlySpan<byte> data)
    {
        Span<byte> header = stackalloc byte[
            ChunkHeaderLength];
        BinaryPrimitives.WriteUInt32BigEndian(
            header,
            checked((uint)data.Length));
        BinaryPrimitives.WriteUInt32BigEndian(
            header[sizeof(uint)..],
            chunkType);
        output.Write(header);
        output.Write(data);
        Span<byte> crc = stackalloc byte[
            ChunkCrcLength];
        BinaryPrimitives.WriteUInt32BigEndian(
            crc,
            PngCrc32.Compute(
                chunkType,
                data));
        output.Write(crc);
    }
}
