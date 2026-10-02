using System.Net;
using System.Net.Http;

namespace Pica.Viewer.Tests.TestDoubles;

internal sealed class RecordingClipboardHttpHandler : HttpMessageHandler
{
    internal List<Uri> RequestedUris { get; } = [];
    internal Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Respond { get; set; } =
        (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[] { 1 }) });

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        RequestedUris.Add(request.RequestUri ?? throw new InvalidOperationException("The request URI is required."));

        return await Respond(request, ct).ConfigureAwait(false);
    }
}
