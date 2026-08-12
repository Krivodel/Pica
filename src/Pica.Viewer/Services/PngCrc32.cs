namespace Pica.Viewer.Services;

internal static class PngCrc32
{
    private const uint Polynomial = 0xEDB88320;

    internal static uint Compute(
        uint chunkType,
        ReadOnlySpan<byte> data)
    {
        uint crc = uint.MaxValue;
        crc = Update(crc, (byte)(chunkType >> 24));
        crc = Update(crc, (byte)(chunkType >> 16));
        crc = Update(crc, (byte)(chunkType >> 8));
        crc = Update(crc, (byte)chunkType);

        foreach (byte value in data)
        {
            crc = Update(crc, value);
        }

        return ~crc;
    }

    private static uint Update(uint crc, byte value)
    {
        crc ^= value;

        for (int bitIndex = 0; bitIndex < 8; bitIndex++)
        {
            crc = (crc & 1) == 0
                ? crc >> 1
                : (crc >> 1) ^ Polynomial;
        }

        return crc;
    }
}
