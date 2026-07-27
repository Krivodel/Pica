using FluentAssertions;
using Xunit;

namespace Pica.Protocol.Tests;

public sealed class PicaProtocolStreamTests
{
    private static readonly Guid ItemId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task WriteAsync_WithViewerRequest_RoundTripsMessage()
    {
        PicaViewerRequest request = new(
            new List<PicaImageItem>
            {
                new(
                    ItemId,
                    @"C:\Images\image.png",
                    "image.png",
                    @"C:\Images\Thumbnails\image.png")
            },
            ItemId,
            new List<PicaActionDefinition>
            {
                new(
                    "sample.action",
                    "Действие",
                    "M0,0 L1,1",
                    0d,
                    PicaActionTargets.CurrentImage,
                    100)
            },
            @"C:\Temp\Pica");
        using MemoryStream stream = new();

        await PicaProtocolStream.WriteAsync(stream, request, CancellationToken.None);
        stream.Position = 0;
        PicaViewerRequest restoredRequest = await PicaProtocolStream
            .ReadAsync<PicaViewerRequest>(stream, CancellationToken.None);

        restoredRequest.Should().BeEquivalentTo(request);
    }

    [Fact]
    public async Task ReadAsync_WithInvalidLength_ThrowsInvalidDataException()
    {
        byte[] invalidHeader = BitConverter.GetBytes(PicaProtocolConstants.MaximumMessageBytes + 1);
        using MemoryStream stream = new(invalidHeader);

        Func<Task> act = async () => await PicaProtocolStream
            .ReadAsync<PicaViewerRequest>(stream, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidDataException>();
    }
}
