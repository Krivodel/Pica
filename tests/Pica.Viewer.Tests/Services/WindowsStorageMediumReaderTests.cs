using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;

using FluentAssertions;
using Xunit;

using Pica.Viewer.Services;
using Pica.Viewer.Tests.TestDoubles;

namespace Pica.Viewer.Tests.Services;

[SupportedOSPlatform("windows")]
public sealed class WindowsStorageMediumReaderTests
{
    private const int MaximumBytes = 1024;
    private const int BufferSize = 2;

    [Theory]
    [InlineData(TYMED.TYMED_HGLOBAL)]
    [InlineData(TYMED.TYMED_ISTREAM)]
    public void ReadIndexedContent_WithVirtualFileStorage_ReturnsBytesAndReleasesStorage(TYMED storageType)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        byte[] content = [1, 2, 3, 4];
        ClipboardStorageDataObject source = new(content, storageType);

        byte[] result = WindowsStorageMediumReader.ReadIndexedContent(source, 1, 0, MaximumBytes, BufferSize, "Input exceeds the test limit.");

        result.Take(content.Length).Should().Equal(content);
        WindowsClipboardAccess.GlobalSize(source.AllocatedMemory).Should().Be(0);
    }

    [Theory]
    [InlineData(TYMED.TYMED_HGLOBAL)]
    [InlineData(TYMED.TYMED_ISTREAM)]
    public void ReadIndexedContent_WithOversizedVirtualFile_ReleasesStorageOnFailure(TYMED storageType)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        ClipboardStorageDataObject source = new(new byte[] { 1, 2, 3, 4 }, storageType);

        Action read = () => WindowsStorageMediumReader.ReadIndexedContent(source, 1, 0, 1, BufferSize, "Input exceeds the test limit.");

        read.Should().Throw<InvalidDataException>();
        WindowsClipboardAccess.GlobalSize(source.AllocatedMemory).Should().Be(0);
    }
}
