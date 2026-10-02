using Microsoft.Extensions.Logging.Abstractions;

using Avalonia.Input;
using FluentAssertions;
using Xunit;

using Pica.Tests.Common;
using Pica.Viewer.Services;
using Pica.Viewer.Tests.TestDoubles;

namespace Pica.Viewer.Tests.Services;

public sealed class ClipboardImageReaderTests
{
    [Fact]
    public async Task ReadAsync_WhenAvaloniaReturnsNoData_UsesIndependentNativeSnapshot()
    {
        byte[] bytes = new byte[] { 1, 2, 3 };
        DelegateClipboardSnapshotReader native = new()
        {
            Capture = () =>
            {
                ClipboardDataSnapshot snapshot = new();
                snapshot.Images.Add(ClipboardImageInput.FromBytes("native.png", bytes));

                return snapshot;
            }
        };
        using ExternalClipboardImageReader external = new();
        ClipboardImageReader reader = CreateReader(native, external);

        IReadOnlyList<ClipboardImageInput> inputs = await reader.ReadAsync(new RecordingClipboard().Clipboard, CancellationToken.None);
        bytes[0] = 99;
        using ClipboardImageInput input = inputs.Should().ContainSingle().Subject;

        (await input.ReadAsync(CancellationToken.None)).Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task ReadAsync_WithNativeRasterAndAvaloniaEncodedData_PrioritizesEncodedRepresentation()
    {
        DelegateClipboardSnapshotReader native = new()
        {
            Capture = () =>
            {
                ClipboardDataSnapshot snapshot = new();
                snapshot.Rasters.Add(ClipboardImageInput.FromBytes("native.bmp", new byte[] { 1 }));

                return snapshot;
            }
        };
        DataTransfer transfer = new();
        transfer.Add(DataTransferItem.Create(DataFormat.CreateBytesPlatformFormat("image/png"), new byte[] { 2 }));
        RecordingClipboard clipboard = new() { Data = transfer };
        using ExternalClipboardImageReader external = new();
        ClipboardImageReader reader = CreateReader(native, external);

        IReadOnlyList<ClipboardImageInput> inputs = await reader.ReadAsync(clipboard.Clipboard, CancellationToken.None);

        inputs.Select(input => input.FileName).Should().Equal("clipboard.png", "native.bmp");

        foreach (ClipboardImageInput input in inputs)
        {
            input.Dispose();
        }
    }

    [Fact]
    public async Task ReadAsync_WithQuotedPathAndHtmlBase_ResolvesFileBeforeRelativeImage()
    {
        using PicaTemporaryDirectory directory = new();
        string path = Path.Combine(directory.DirectoryPath, "image.png");
        await File.WriteAllBytesAsync(path, new byte[] { 1 });
        DataTransfer transfer = new();
        transfer.Add(DataTransferItem.CreateText($"\"{path}\""));
        transfer.Add(DataTransferItem.Create(DataFormat.CreateStringPlatformFormat("HTML Format"),
            "SourceURL:https://example.test/article/page\r\n<base href='../images/'><img title='a > b' src='photo.png?x=1&amp;y=2'>"));
        RecordingClipboard clipboard = new() { Data = transfer };
        RecordingClipboardHttpHandler handler = new();
        using ExternalClipboardImageReader external = new(handler);
        ClipboardImageReader reader = CreateReader(new DelegateClipboardSnapshotReader(), external);

        IReadOnlyList<ClipboardImageInput> inputs = await reader.ReadAsync(clipboard.Clipboard, CancellationToken.None);

        inputs.Should().HaveCount(2);
        inputs[0].FilePath.Should().Be(path);
        (await inputs[1].ReadAsync(CancellationToken.None)).Should().Equal(1);
        handler.RequestedUris.Should().Equal(new Uri("https://example.test/images/photo.png?x=1&y=2"));

        foreach (ClipboardImageInput input in inputs)
        {
            input.Dispose();
        }
    }

    [Theory]
    [InlineData("<img src='a>b.png'><IMG SRC=second.png>", "a>b.png", "second.png")]
    [InlineData("<image src='wrong.png'><img data-src='ignored.png' src='correct.png'>", "correct.png", null)]
    [InlineData("<!-- <img src='comment.png'> --><script>const a = '<img src=script.png>';</script><img hidden src='correct.png'>", "correct.png", null)]
    public void ExtractSources_WithAttributes_ReadsOnlyImageSources(string html, string first, string? second)
    {
        IReadOnlyList<string> sources = HtmlImageSourceExtractor.ExtractSources(html);

        sources.Should().Equal(second is null ? new string[] { first } : new string[] { first, second });
    }

    [Fact]
    public async Task ReadAsync_WithLargeDataUri_DoesNotUseUriLengthLimitedParsing()
    {
        byte[] content = new byte[128 * 1024];
        content[content.Length - 1] = 123;
        DataTransfer transfer = new();
        transfer.Add(DataTransferItem.CreateText("data:image/png;base64," + Convert.ToBase64String(content)));
        RecordingClipboard clipboard = new() { Data = transfer };
        using ExternalClipboardImageReader external = new();
        ClipboardImageReader reader = CreateReader(new DelegateClipboardSnapshotReader(), external);

        IReadOnlyList<ClipboardImageInput> inputs = await reader.ReadAsync(clipboard.Clipboard, CancellationToken.None);
        using ClipboardImageInput input = inputs.Should().ContainSingle().Subject;

        (await input.ReadAsync(CancellationToken.None)).Should().Equal(content);
    }

    private static ClipboardImageReader CreateReader(IPlatformClipboardSnapshotReader native, ExternalClipboardImageReader external)
    {
        return new ClipboardImageReader(new ClipboardImageFormatCatalog(new ImageFormatRegistry()), native, external,
            NullLogger<ClipboardImageReader>.Instance, new InlineViewerUiDispatcher());
    }
}
