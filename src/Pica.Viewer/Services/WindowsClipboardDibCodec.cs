using System.Buffers.Binary;

namespace Pica.Viewer.Services;

internal static class WindowsClipboardDibCodec
{
    private const int BitmapFileHeaderSize = 14;
    private const int BitmapInfoHeaderSize = 40;
    private const int BitmapCoreHeaderSize = 12;
    private const int BitmapV5HeaderSize = 124;
    private const int BitmapSignature = 0x4D42;
    private const uint BitmapCompressionBitFields = 3;
    private const uint BitmapCompressionAlphaBitFields = 6;

    internal static byte[] CreateBitmapFile(byte[] dibContent)
    {
        ArgumentNullException.ThrowIfNull(dibContent);

        int pixelOffset = GetPixelOffset(dibContent);
        int fileSize = checked(BitmapFileHeaderSize + dibContent.Length);
        byte[] bitmapFile = new byte[fileSize];
        Span<byte> header = bitmapFile.AsSpan(0, BitmapFileHeaderSize);
        BinaryPrimitives.WriteUInt16LittleEndian(header, BitmapSignature);
        BinaryPrimitives.WriteUInt32LittleEndian(header[2..], checked((uint)fileSize));
        BinaryPrimitives.WriteUInt32LittleEndian(
            header[10..],
            checked((uint)(BitmapFileHeaderSize + pixelOffset)));
        dibContent.CopyTo(bitmapFile, BitmapFileHeaderSize);

        return bitmapFile;
    }

    private static int GetPixelOffset(ReadOnlySpan<byte> dibContent)
    {
        if (dibContent.Length < sizeof(uint))
        {
            throw new InvalidDataException("The clipboard bitmap header is incomplete.");
        }

        uint headerSize = BinaryPrimitives.ReadUInt32LittleEndian(dibContent);

        return headerSize switch
        {
            BitmapCoreHeaderSize => GetCorePixelOffset(dibContent),
            >= BitmapInfoHeaderSize => GetInfoPixelOffset(dibContent, headerSize),
            _ => throw new InvalidDataException("The clipboard bitmap header is unsupported.")
        };
    }

    private static int GetCorePixelOffset(ReadOnlySpan<byte> dibContent)
    {
        if (dibContent.Length < BitmapCoreHeaderSize)
        {
            throw new InvalidDataException("The clipboard bitmap header is incomplete.");
        }

        ushort bitsPerPixel = BinaryPrimitives.ReadUInt16LittleEndian(dibContent[10..]);
        int colorCount = GetDefaultColorCount(bitsPerPixel);

        return ValidatePixelOffset(
            dibContent,
            checked(BitmapCoreHeaderSize + (colorCount * 3)));
    }

    private static int GetInfoPixelOffset(
        ReadOnlySpan<byte> dibContent,
        uint headerSize)
    {
        if (headerSize > int.MaxValue || dibContent.Length < headerSize)
        {
            throw new InvalidDataException("The clipboard bitmap header is incomplete.");
        }

        ushort bitsPerPixel = BinaryPrimitives.ReadUInt16LittleEndian(dibContent[14..]);
        uint compression = BinaryPrimitives.ReadUInt32LittleEndian(dibContent[16..]);
        uint usedColorCount = BinaryPrimitives.ReadUInt32LittleEndian(dibContent[32..]);
        int colorCount = usedColorCount == 0
            ? GetDefaultColorCount(bitsPerPixel)
            : checked((int)usedColorCount);
        int externalMaskBytes = headerSize == BitmapInfoHeaderSize
            ? compression switch
            {
                BitmapCompressionBitFields => 3 * sizeof(uint),
                BitmapCompressionAlphaBitFields => 4 * sizeof(uint),
                _ => 0
            }
            : 0;
        int minimumOffset = checked(
            (int)headerSize
            + externalMaskBytes
            + (colorCount * sizeof(uint)));

        if (headerSize >= BitmapV5HeaderSize)
        {
            uint profileOffset = BinaryPrimitives.ReadUInt32LittleEndian(dibContent[112..]);
            uint profileSize = BinaryPrimitives.ReadUInt32LittleEndian(dibContent[116..]);

            if (profileOffset > 0 && profileSize > 0)
            {
                minimumOffset = Math.Max(
                    minimumOffset,
                    checked((int)(profileOffset + profileSize)));
            }
        }

        uint imageSize = BinaryPrimitives.ReadUInt32LittleEndian(dibContent[20..]);
        int offsetFromImageSize = imageSize > 0 && imageSize <= dibContent.Length
            ? dibContent.Length - checked((int)imageSize)
            : minimumOffset;

        return ValidatePixelOffset(
            dibContent,
            Math.Max(minimumOffset, offsetFromImageSize));
    }

    private static int GetDefaultColorCount(ushort bitsPerPixel)
    {
        return bitsPerPixel is > 0 and <= 8
            ? 1 << bitsPerPixel
            : 0;
    }

    private static int ValidatePixelOffset(
        ReadOnlySpan<byte> dibContent,
        int pixelOffset)
    {
        if (pixelOffset < 0 || pixelOffset >= dibContent.Length)
        {
            throw new InvalidDataException("The clipboard bitmap contains no pixel data.");
        }

        return pixelOffset;
    }
}
