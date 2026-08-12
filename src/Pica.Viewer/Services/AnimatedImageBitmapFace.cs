using AnimatedImage;

namespace Pica.Viewer.Services;

internal sealed class AnimatedImageBitmapFace : IBitmapFace
{
    internal int Width { get; }
    internal int Height { get; }

    private const int BytesPerPixel = 4;

    private readonly byte[] _pixels;

    internal AnimatedImageBitmapFace(
        int width,
        int height)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(width),
                width,
                "The image width must be positive.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(height),
                height,
                "The image height must be positive.");
        }

        Width = width;
        Height = height;
        _pixels = new byte[checked(
            width * height * BytesPerPixel)];
    }

    public void WriteBGRA(
        byte[] buffer,
        int x,
        int y,
        int width,
        int height)
    {
        ValidateAreaAndBuffer(
            buffer,
            x,
            y,
            width,
            height);
        CopyRows(
            buffer,
            0,
            _pixels,
            GetPixelOffset(x, y),
            width,
            height);
    }

    public void ReadBGRA(
        byte[] buffer,
        int x,
        int y,
        int width,
        int height)
    {
        ValidateAreaAndBuffer(
            buffer,
            x,
            y,
            width,
            height);
        CopyRows(
            _pixels,
            GetPixelOffset(x, y),
            buffer,
            0,
            width,
            height);
    }

    public void Clear(
        int x,
        int y,
        int width,
        int height)
    {
        ValidateArea(x, y, width, height);
        int rowLength = checked(width * BytesPerPixel);
        int offset = GetPixelOffset(x, y);
        int stride = checked(Width * BytesPerPixel);

        for (int row = 0; row < height; row++)
        {
            Array.Clear(
                _pixels,
                offset + (row * stride),
                rowLength);
        }
    }

    internal byte[] CopyPixels()
    {
        return (byte[])_pixels.Clone();
    }

    private static void CopyRows(
        byte[] source,
        int sourceOffset,
        byte[] destination,
        int destinationOffset,
        int width,
        int height,
        int? sourceStride = null,
        int? destinationStride = null)
    {
        int rowLength = checked(width * BytesPerPixel);
        int effectiveSourceStride =
            sourceStride ?? rowLength;
        int effectiveDestinationStride =
            destinationStride ?? rowLength;

        for (int row = 0; row < height; row++)
        {
            Buffer.BlockCopy(
                source,
                sourceOffset
                    + (row * effectiveSourceStride),
                destination,
                destinationOffset
                    + (row * effectiveDestinationStride),
                rowLength);
        }
    }

    private void CopyRows(
        byte[] source,
        int sourceOffset,
        byte[] destination,
        int destinationOffset,
        int width,
        int height)
    {
        int canvasStride = checked(
            Width * BytesPerPixel);
        bool sourceIsCanvas = object.ReferenceEquals(
            source,
            _pixels);

        CopyRows(
            source,
            sourceOffset,
            destination,
            destinationOffset,
            width,
            height,
            sourceIsCanvas ? canvasStride : null,
            sourceIsCanvas ? null : canvasStride);
    }

    private void ValidateAreaAndBuffer(
        byte[] buffer,
        int x,
        int y,
        int width,
        int height)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ValidateArea(x, y, width, height);
        int requiredLength = checked(
            width * height * BytesPerPixel);

        if (buffer.Length < requiredLength)
        {
            throw new ArgumentException(
                $"The pixel buffer has length {buffer.Length}, expected at least {requiredLength}.",
                nameof(buffer));
        }
    }

    private void ValidateArea(
        int x,
        int y,
        int width,
        int height)
    {
        if ((x < 0)
            || (y < 0)
            || (width <= 0)
            || (height <= 0)
            || (x > Width - width)
            || (y > Height - height))
        {
            throw new ArgumentOutOfRangeException(
                nameof(width),
                "The pixel area must fit inside the image.");
        }
    }

    private int GetPixelOffset(int x, int y)
    {
        return checked(
            ((y * Width) + x) * BytesPerPixel);
    }
}
