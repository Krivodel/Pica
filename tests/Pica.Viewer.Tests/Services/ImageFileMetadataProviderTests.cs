using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;
using Xunit;

using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.Services;

public sealed class ImageFileMetadataProviderTests
{
    private static readonly DateTime ModificationDate =
        new(2026, 7, 29, 12, 30, 0, DateTimeKind.Local);

    [Fact]
    public async Task GetModificationDateAsync_WithExistingFile_ReturnsDate()
    {
        string filePath = Path.GetTempFileName();
        ImageFileMetadataProvider provider = new(
            NullLogger<ImageFileMetadataProvider>.Instance);

        try
        {
            File.SetLastWriteTime(filePath, ModificationDate);

            DateTime? result = await provider.GetModificationDateAsync(
                filePath,
                CancellationToken.None);

            result.Should().Be(ModificationDate);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task GetModificationDateAsync_WithMissingFile_ReturnsNull()
    {
        string filePath = Path.Combine(
            Path.GetTempPath(),
            $"Pica-missing-metadata-{Guid.NewGuid():N}.png");
        ImageFileMetadataProvider provider = new(
            NullLogger<ImageFileMetadataProvider>.Instance);

        DateTime? result = await provider.GetModificationDateAsync(
            filePath,
            CancellationToken.None);

        result.Should().BeNull();
    }
}
