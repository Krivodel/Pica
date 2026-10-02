namespace Pica.Viewer.Services;

internal static class ImageStreamBuffer
{
    internal const int CopyBufferSize = 81920;

    internal static async Task<byte[]> ReadAllBytesAsync(Stream input, int maxBytes, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);
        using MemoryStream output = new();
        byte[] buffer = new byte[CopyBufferSize];

        while (true)
        {
            int count = await input.ReadAsync(buffer, ct).ConfigureAwait(false);

            if (count == 0)
            {
                return output.ToArray();
            }

            if (output.Length + count > maxBytes)
            {
                throw new InvalidDataException("The image content exceeds the input size limit.");
            }

            output.Write(buffer, 0, count);
        }
    }

    internal static MemoryStream CopyToMemory(
        Stream sourceStream,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sourceStream);

        if ((sourceStream is MemoryStream memoryStream)
            && memoryStream.TryGetBuffer(
                out ArraySegment<byte> sourceBuffer)
            && (sourceBuffer.Array is byte[] sourceData))
        {
            int sourcePosition = checked(
                (int)memoryStream.Position);
            int remainingLength = checked(
                (int)(memoryStream.Length
                    - memoryStream.Position));

            return new MemoryStream(
                sourceData,
                sourceBuffer.Offset + sourcePosition,
                remainingLength,
                writable: false,
                publiclyVisible: true);
        }

        MemoryStream bufferedStream = new();
        byte[] buffer = new byte[CopyBufferSize];

        try
        {
            int bytesRead;

            while ((bytesRead = sourceStream.Read(
                    buffer,
                    0,
                    buffer.Length))
                > 0)
            {
                ct.ThrowIfCancellationRequested();
                bufferedStream.Write(
                    buffer,
                    0,
                    bytesRead);
            }

            bufferedStream.Position = 0;

            return bufferedStream;
        }
        catch
        {
            bufferedStream.Dispose();
            throw;
        }
    }
}
