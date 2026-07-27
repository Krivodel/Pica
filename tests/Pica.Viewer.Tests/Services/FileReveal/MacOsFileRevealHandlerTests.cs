using FluentAssertions;
using Xunit;

using Pica.Viewer.Services;
using Pica.Viewer.Services.FileReveal;
using Pica.Viewer.Tests.TestDoubles;

namespace Pica.Viewer.Tests.Services.FileReveal;

public sealed class MacOsFileRevealHandlerTests
{
    private const string FilePath = "/tmp/Pica/image.png";

    [Fact]
    public void Reveal_WithReuseExistingMode_SearchesFinderWindows()
    {
        RecordingFileRevealProcessLauncher processLauncher = new();
        MacOsFileRevealHandler handler = new(processLauncher);

        handler.Reveal(FilePath, FileRevealWindowMode.ReuseExisting);

        processLauncher.ExecutablePath.Should().Be("/usr/bin/osascript");
        processLauncher.Arguments.Should()
            .Contain("repeat with openWindow in Finder windows");
        processLauncher.Arguments.Should().Contain(FilePath);
    }

    [Fact]
    public void Reveal_WithOpenNewMode_CreatesNewFinderWindow()
    {
        RecordingFileRevealProcessLauncher processLauncher = new();
        MacOsFileRevealHandler handler = new(processLauncher);

        handler.Reveal(FilePath, FileRevealWindowMode.OpenNew);

        processLauncher.ExecutablePath.Should().Be("/usr/bin/osascript");
        processLauncher.Arguments.Should().Contain(FilePath);
        processLauncher.Arguments.Should()
            .Contain("set targetWindow to make new Finder window");
        processLauncher.Arguments.Should()
            .NotContain("repeat with openWindow in Finder windows");
    }
}
