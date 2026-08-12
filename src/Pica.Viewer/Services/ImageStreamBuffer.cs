namespace Pica.Viewer.Services;

internal static class ImageStreamBuffer
{
    private const int CopyBufferSize = 81920;

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
