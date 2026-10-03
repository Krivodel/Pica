namespace Pica.Viewer.Tests.Services;

internal static class TgaImageTestData
{
    internal const int Width = 2;
    internal const int Height = 2;
    internal const int HeaderLength = 18;

    internal static byte[] Create(int pixelDepth, bool compressed, byte origin)
    {
        byte[] header =
        [
            0, 0, compressed ? (byte)10 : (byte)2,
            0, 0, 0, 0, 0, 0, 0, 0, 0,
            Width, 0, Height, 0, (byte)pixelDepth,
            (byte)(origin | (pixelDepth == 32 ? 8 : 0))
        ];
        List<byte> content = new(header);
        byte[] pixels = GetExpectedPixels(pixelDepth);
        int bytesPerPixel = pixelDepth / 8;

        for (int row = 0; row < Height; row++)
        {
            int sourceRow = (origin & 0x20) == 0 ? Height - row - 1 : row;
            bool repeatedRow = compressed && (sourceRow == 1);

            if (compressed)
            {
                content.Add(repeatedRow ? (byte)0x81 : (byte)0x01);
            }

            int pixelCount = repeatedRow ? 1 : Width;

            for (int column = 0; column < pixelCount; column++)
            {
                int sourceColumn = (origin & 0x10) == 0 ? column : Width - column - 1;
                int pixelOffset = (sourceRow * Width + sourceColumn) * 4;
                content.AddRange(pixels.AsSpan(pixelOffset, bytesPerPixel).ToArray());
            }
        }

        return content.ToArray();
    }

    internal static byte[] GetExpectedPixels(int pixelDepth)
    {
        byte[] pixels =
        [
            0, 0, 255, pixelDepth == 32 ? (byte)0 : (byte)255,
            0, 255, 0, pixelDepth == 32 ? (byte)128 : (byte)255,
            255, 0, 0, 255, 255, 0, 0, 255
        ];

        return pixels;
    }

    internal static (byte[] Content, byte[] Pixels) CreateEightBit(bool colorMapped)
    {
        byte[] header =
        [
            0, colorMapped ? (byte)1 : (byte)0, colorMapped ? (byte)1 : (byte)3,
            0, 0, colorMapped ? (byte)2 : (byte)0, 0, colorMapped ? (byte)24 : (byte)0,
            0, 0, 0, 0, Width, 0, Height, 0, 8, 0x20
        ];
        byte[] data = colorMapped
            ? new byte[] { 0, 0, 255, 255, 0, 0, 0, 1, 1, 0 }
            : new byte[] { 0, 85, 170, 255 };
        byte[] pixels = colorMapped
            ? new byte[] { 0, 0, 255, 255, 255, 0, 0, 255, 255, 0, 0, 255, 0, 0, 255, 255 }
            : new byte[] { 0, 0, 0, 255, 85, 85, 85, 255, 170, 170, 170, 255, 255, 255, 255, 255 };

        return (header.Concat(data).ToArray(), pixels);
    }
}
