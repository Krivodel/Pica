using System.Buffers.Binary;

namespace Pica.Viewer.Services;

internal static class WindowsDibV5Builder
{
    internal const int HeaderSize = 124;

    private const uint BitFieldsCompression = 3;
    private const uint RedMask = 0x00FF0000;
    private const uint GreenMask = 0x0000FF00;
    private const uint BlueMask = 0x000000FF;
    private const uint AlphaMask = 0xFF000000;
    private const uint SrgbColorSpace = 0x73524742;
    private const uint ImagesRenderingIntent = 4;

    public static byte[] Build(PreparedClipboardBitmap image)
    {
        ArgumentNullException.ThrowIfNull(image);

        int imageSize = checked(image.RowBytes * image.PixelSize.Height);

        if (image.BgraPixels.Length != imageSize)
        {
            throw new InvalidDataException(
                "The selected-area pixel buffer size does not match the image dimensions.");
        }

        byte[] content = new byte[checked(HeaderSize + imageSize)];
        Span<byte> header = content.AsSpan(0, HeaderSize);
        BinaryPrimitives.WriteUInt32LittleEndian(header[0..4], HeaderSize);
        BinaryPrimitives.WriteInt32LittleEndian(header[4..8], image.PixelSize.Width);
        BinaryPrimitives.WriteInt32LittleEndian(header[8..12], -image.PixelSize.Height);
        BinaryPrimitives.WriteUInt16LittleEndian(header[12..14], 1);
        BinaryPrimitives.WriteUInt16LittleEndian(header[14..16], 32);
        BinaryPrimitives.WriteUInt32LittleEndian(header[16..20], BitFieldsCompression);
        BinaryPrimitives.WriteUInt32LittleEndian(header[20..24], (uint)imageSize);
        BinaryPrimitives.WriteUInt32LittleEndian(header[40..44], RedMask);
        BinaryPrimitives.WriteUInt32LittleEndian(header[44..48], GreenMask);
        BinaryPrimitives.WriteUInt32LittleEndian(header[48..52], BlueMask);
        BinaryPrimitives.WriteUInt32LittleEndian(header[52..56], AlphaMask);
        BinaryPrimitives.WriteUInt32LittleEndian(header[56..60], SrgbColorSpace);
        BinaryPrimitives.WriteUInt32LittleEndian(header[108..112], ImagesRenderingIntent);
        image.BgraPixels.CopyTo(content, HeaderSize);

        return content;
    }
}
