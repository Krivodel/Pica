using System.Net;
using System.Net.Http;

namespace Pica.Viewer.Services;

internal sealed class ExternalClipboardImageReader : IDisposable
{
    private readonly HttpClient _client;

    public ExternalClipboardImageReader()
    {
        _client = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false
        });
    }

    internal ExternalClipboardImageReader(HttpMessageHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _client = new HttpClient(handler);
    }

    internal ClipboardImageInput CreateInput(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        return ClipboardImageInput.FromReader(ct => ReadAsync(uri, ct));
    }

    internal ClipboardImageInput CreateDataInput(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return ClipboardImageInput.FromReader(ct => Task.Run(() => ReadDataUri(value), ct));
    }

    internal async Task<byte[]> ReadAsync(Uri uri, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ct.ThrowIfCancellationRequested();

        if (uri.Scheme == "data")
        {
            return await Task.Run(() => ReadDataUri(uri.OriginalString), ct).ConfigureAwait(false);
        }

        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(ClipboardImageLimits.DownloadTimeout);
        Uri currentUri = uri;

        for (int redirect = 0; redirect <= ClipboardImageLimits.MaximumRedirects; redirect++)
        {
            if ((currentUri.Scheme != Uri.UriSchemeHttp) && (currentUri.Scheme != Uri.UriSchemeHttps))
            {
                throw new InvalidDataException("The clipboard link does not use HTTP or HTTPS.");
            }

            using HttpRequestMessage request = new(HttpMethod.Get, currentUri);
            using HttpResponseMessage response = await _client.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);

            if ((response.StatusCode is HttpStatusCode.MovedPermanently or HttpStatusCode.Found
                    or HttpStatusCode.SeeOther or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect)
                && (response.Headers.Location is { } location))
            {
                currentUri = new Uri(currentUri, location);

                continue;
            }

            response.EnsureSuccessStatusCode();

            if (response.Content.Headers.ContentLength > ClipboardImageLimits.MaximumInputBytes)
            {
                throw new InvalidDataException("The downloaded image exceeds the input size limit.");
            }

            await using Stream stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);

            return await ImageStreamBuffer.ReadAllBytesAsync(
                stream, ClipboardImageLimits.MaximumInputBytes, timeout.Token).ConfigureAwait(false);
        }

        throw new InvalidDataException("The clipboard link exceeded the redirect limit.");
    }

    public void Dispose()
    {
        _client.Dispose();
    }

    private static byte[] ReadDataUri(string value)
    {
        int separator = value.IndexOf(',');

        if ((separator < 0) || !value.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The clipboard data URI is invalid.");
        }

        string metadata = value[..separator];
        string data = value[(separator + 1)..];
        bool isBase64 = metadata.EndsWith(";base64", StringComparison.OrdinalIgnoreCase);
        long maximumEncodedLength = isBase64
            ? ((long)ClipboardImageLimits.MaximumInputBytes + 2) / 3 * 4
            : (long)ClipboardImageLimits.MaximumInputBytes * 3;

        if (data.Length > maximumEncodedLength)
        {
            throw new InvalidDataException("The clipboard data URI exceeds the input size limit.");
        }

        byte[] content = isBase64
            ? Convert.FromBase64String(Uri.UnescapeDataString(data))
            : DecodePercentBytes(data);

        if (content.Length > ClipboardImageLimits.MaximumInputBytes)
        {
            throw new InvalidDataException("The clipboard image exceeds the input size limit.");
        }

        return content;
    }

    private static byte[] DecodePercentBytes(string data)
    {
        using MemoryStream stream = new();

        for (int index = 0; index < data.Length; index++)
        {
            if (stream.Length >= ClipboardImageLimits.MaximumInputBytes)
            {
                throw new InvalidDataException("The clipboard image exceeds the input size limit.");
            }

            if ((data[index] == '%') && (index + 2 < data.Length))
            {
                stream.WriteByte(Convert.ToByte(data.Substring(index + 1, 2), 16));
                index += 2;
            }
            else if (data[index] <= byte.MaxValue)
            {
                stream.WriteByte((byte)data[index]);
            }
            else
            {
                throw new InvalidDataException("The clipboard data URI contains invalid binary data.");
            }
        }

        return stream.ToArray();
    }
}
