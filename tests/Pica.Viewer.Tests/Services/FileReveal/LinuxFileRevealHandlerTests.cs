using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;
using Xunit;

using Pica.Viewer.Services;
using Pica.Viewer.Services.FileReveal;
using Pica.Viewer.Tests.TestDoubles;

namespace Pica.Viewer.Tests.Services.FileReveal;

public sealed class LinuxFileRevealHandlerTests
{
    private const string FilePath = "/tmp/Pica/image.png";

    [Fact]
    public void Reveal_WithReuseExistingMode_RequestsFileManagerSelection()
    {
        RecordingStandardFileRevealer standardRevealer = new();
        RecordingFileRevealProcessLauncher processLauncher = new();
        LinuxFileRevealHandler handler = CreateHandler(
            standardRevealer,
            processLauncher);

        handler.Reveal(FilePath, FileRevealWindowMode.ReuseExisting);

        standardRevealer.CallCount.Should().Be(0);
        processLauncher.ExecutablePath.Should().Be("dbus-send");
        processLauncher.Arguments.Should()
            .Contain("org.freedesktop.FileManager1.ShowItems");
        processLauncher.Arguments.Should()
            .Contain("array:string:file:///tmp/Pica/image.png");
    }

    [Fact]
    public void Reveal_WithOpenNewMode_UsesStandardDirectoryOpener()
    {
        RecordingStandardFileRevealer standardRevealer = new();
        RecordingFileRevealProcessLauncher processLauncher = new();
        LinuxFileRevealHandler handler = CreateHandler(
            standardRevealer,
            processLauncher);

        handler.Reveal(FilePath, FileRevealWindowMode.OpenNew);

        standardRevealer.FilePath.Should().Be(FilePath);
        processLauncher.CallCount.Should().Be(0);
    }

    private static LinuxFileRevealHandler CreateHandler(
        IStandardFileRevealer standardRevealer,
        IFileRevealProcessLauncher processLauncher)
    {
        return new LinuxFileRevealHandler(
            standardRevealer,
            processLauncher,
            NullLogger<LinuxFileRevealHandler>.Instance);
    }
}
