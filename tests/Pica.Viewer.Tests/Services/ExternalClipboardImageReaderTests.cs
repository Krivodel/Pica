using System.Net;
using System.Net.Http;

using FluentAssertions;
using Xunit;

using Pica.Viewer.Services;
using Pica.Viewer.Tests.TestDoubles;

namespace Pica.Viewer.Tests.Services;

public sealed class ExternalClipboardImageReaderTests
{
    [Theory]
    [InlineData("data:image/png;base64,AQID")]
    [InlineData("data:image/png,%01%02%03")]
    public async Task ReadAsync_WithDataUri_ReturnsEncodedBytes(string uri)
    {
        using ExternalClipboardImageReader reader = new();

        byte[] content = await reader.ReadAsync(new Uri(uri), CancellationToken.None);

        content.Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task ReadAsync_WithRelativeRedirect_FollowsImageWithinLimit()
    {
        RecordingClipboardHttpHandler handler = new();
        handler.Respond = (_, _) =>
        {
            HttpResponseMessage response = handler.RequestedUris.Count == 1
                ? new HttpResponseMessage(HttpStatusCode.Found)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[] { 1, 2 }) };

            if (handler.RequestedUris.Count == 1)
            {
                response.Headers.Location = new Uri("../image.png", UriKind.Relative);
            }

            return Task.FromResult(response);
        };
        using ExternalClipboardImageReader reader = new(handler);

        byte[] content = await reader.ReadAsync(new Uri("https://example.test/path/redirect"), CancellationToken.None);

        content.Should().Equal(1, 2);
        handler.RequestedUris.Select(uri => uri.AbsoluteUri).Should().Equal(
            "https://example.test/path/redirect", "https://example.test/image.png");
    }

    [Fact]
    public async Task ReadAsync_WithRedirectLoop_StopsAfterFiveRedirects()
    {
        RecordingClipboardHttpHandler handler = new()
        {
            Respond = (request, _) =>
            {
                HttpResponseMessage response = new(HttpStatusCode.Found);
                response.Headers.Location = request.RequestUri;

                return Task.FromResult(response);
            }
        };
        using ExternalClipboardImageReader reader = new(handler);

        Func<Task> read = () => reader.ReadAsync(new Uri("https://example.test/image"), CancellationToken.None);

        await read.Should().ThrowAsync<InvalidDataException>();
        handler.RequestedUris.Should().HaveCount(6);
    }

    [Fact]
    public async Task ReadAsync_WhenRequestFails_PropagatesNetworkFailure()
    {
        RecordingClipboardHttpHandler handler = new()
        {
            Respond = (_, _) => throw new HttpRequestException("The test server is unavailable.")
        };
        using ExternalClipboardImageReader reader = new(handler);

        Func<Task> read = () => reader.ReadAsync(new Uri("https://example.test/image"), CancellationToken.None);

        await read.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task ReadAsync_WhenCancelledWhileDownloading_CancelsRequest()
    {
        TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        RecordingClipboardHttpHandler handler = new()
        {
            Respond = async (_, ct) =>
            {
                started.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, ct);

                return new HttpResponseMessage(HttpStatusCode.OK);
            }
        };
        using ExternalClipboardImageReader reader = new(handler);
        using CancellationTokenSource cancellation = new();

        Task<byte[]> reading = reader.ReadAsync(new Uri("https://example.test/image"), cancellation.Token);
        await started.Task;
        cancellation.Cancel();
        Func<Task> read = () => reading;

        await read.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ReadAsync_WithRedirectToFile_RejectsNonHttpDestination()
    {
        RecordingClipboardHttpHandler handler = new()
        {
            Respond = (_, _) =>
            {
                HttpResponseMessage response = new(HttpStatusCode.Found);
                response.Headers.Location = new Uri("file:///C:/image.png");

                return Task.FromResult(response);
            }
        };
        using ExternalClipboardImageReader reader = new(handler);

        Func<Task> read = () => reader.ReadAsync(new Uri("https://example.test/image"), CancellationToken.None);

        await read.Should().ThrowAsync<InvalidDataException>();
        handler.RequestedUris.Should().ContainSingle();
    }

    [Fact]
    public async Task ReadAsync_WithOversizedResponse_RejectsBeforeReadingBody()
    {
        RecordingClipboardHttpHandler handler = new()
        {
            Respond = (_, _) =>
            {
                HttpResponseMessage response = new(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[] { 1 }) };
                response.Content.Headers.ContentLength = (long)ClipboardImageLimits.MaximumInputBytes + 1;

                return Task.FromResult(response);
            }
        };
        using ExternalClipboardImageReader reader = new(handler);

        Func<Task> read = () => reader.ReadAsync(new Uri("https://example.test/image"), CancellationToken.None);

        await read.Should().ThrowAsync<InvalidDataException>();
    }
}
