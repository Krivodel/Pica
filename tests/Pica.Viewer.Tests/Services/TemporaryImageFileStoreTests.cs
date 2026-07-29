using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;
using Xunit;

using Pica.Tests.Common;
using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.Services;

public sealed class TemporaryImageFileStoreTests
{
    [Fact]
    public async Task SaveAsync_WhenStoreAlreadyDisposed_RemovesLateFile()
    {
        using PicaTemporaryDirectory temporaryDirectory = new();
        string filePath = Path.Combine(
            temporaryDirectory.DirectoryPath,
            "late-image.png");
        using TemporaryImageFileStore store = new(
            NullLogger<TemporaryImageFileStore>.Instance);
        PreparedClipboardImage image = CreateImage();
        store.Dispose();

        await store.SaveAsync(
            filePath,
            image,
            CancellationToken.None);

        File.Exists(filePath).Should().BeFalse();
    }

    [Fact]
    public async Task Dispose_WithRegisteredFile_RemovesFile()
    {
        using PicaTemporaryDirectory temporaryDirectory = new();
        string filePath = Path.Combine(
            temporaryDirectory.DirectoryPath,
            "registered-image.png");
        using TemporaryImageFileStore store = new(
            NullLogger<TemporaryImageFileStore>.Instance);
        PreparedClipboardImage image = CreateImage();
        await store.SaveAsync(
            filePath,
            image,
            CancellationToken.None);

        store.Dispose();

        File.Exists(filePath).Should().BeFalse();
    }

    private static PreparedClipboardImage CreateImage()
    {
        return new PreparedClipboardImage(
            new ImageDimensions(1, 1),
            4,
            new byte[] { 0, 0, 0, 255 },
            new byte[] { 1, 2, 3 });
    }
}
