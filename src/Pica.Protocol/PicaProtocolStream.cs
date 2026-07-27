using System.Buffers.Binary;
using System.Text.Json;

namespace Pica.Protocol;

public static class PicaProtocolStream
{
    private const int HeaderSize = sizeof(int);

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static async Task WriteAsync<T>(Stream stream, T payload, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(payload);

        PicaProtocolMessage<T> message = new(PicaProtocolConstants.CurrentVersion, payload);
        byte[] content = JsonSerializer.SerializeToUtf8Bytes(message, SerializerOptions);

        if (content.Length > PicaProtocolConstants.MaximumMessageBytes)
        {
            throw new InvalidDataException(
                $"Pica protocol message contains {content.Length} bytes, which exceeds the allowed limit.");
        }

        byte[] header = new byte[HeaderSize];
        BinaryPrimitives.WriteInt32LittleEndian(header, content.Length);
        await stream.WriteAsync(header, ct).ConfigureAwait(false);
        await stream.WriteAsync(content, ct).ConfigureAwait(false);
        await stream.FlushAsync(ct).ConfigureAwait(false);
    }

    public static async Task<T> ReadAsync<T>(Stream stream, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(stream);

        byte[] header = new byte[HeaderSize];
        await ReadExactlyAsync(stream, header, ct).ConfigureAwait(false);
        int contentLength = BinaryPrimitives.ReadInt32LittleEndian(header);

        if ((contentLength <= 0) || (contentLength > PicaProtocolConstants.MaximumMessageBytes))
        {
            throw new InvalidDataException($"Pica protocol message length '{contentLength}' is invalid.");
        }

        byte[] content = new byte[contentLength];
        await ReadExactlyAsync(stream, content, ct).ConfigureAwait(false);
        PicaProtocolMessage<T>? message = JsonSerializer.Deserialize<PicaProtocolMessage<T>>(
            content,
            SerializerOptions);

        if (message is null)
        {
            throw new InvalidDataException("Pica protocol message is empty.");
        }

        if (message.Version != PicaProtocolConstants.CurrentVersion)
        {
            throw new InvalidDataException(
                $"Pica protocol version '{message.Version}' is not supported.");
        }

        return message.Payload;
    }

    private static async Task ReadExactlyAsync(Stream stream, Memory<byte> buffer, CancellationToken ct)
    {
        int totalBytesRead = 0;

        while (totalBytesRead < buffer.Length)
        {
            int bytesRead = await stream
                .ReadAsync(buffer[totalBytesRead..], ct)
                .ConfigureAwait(false);

            if (bytesRead == 0)
            {
                throw new EndOfStreamException("The Pica protocol stream ended before the message was complete.");
            }

            totalBytesRead += bytesRead;
        }
    }
}
