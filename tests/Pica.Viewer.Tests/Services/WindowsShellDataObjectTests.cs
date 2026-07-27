using FluentAssertions;
using Xunit;

using Pica.Tests.Common;
using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.Services;

public sealed class WindowsShellDataObjectTests
{
    [Fact]
    public void Create_WithExistingFile_ReturnsSystemDataObject()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using PicaTemporaryDirectory temporaryDirectory = new();
        string filePath = Path.Combine(
            temporaryDirectory.DirectoryPath,
            "image.png");
        File.WriteAllBytes(filePath, new byte[] { 1, 2, 3, 4 });

        using WindowsShellDataObject dataObject = WindowsShellDataObject.Create(filePath);

        dataObject.Pointer.Should().NotBe(nint.Zero);
    }
}
