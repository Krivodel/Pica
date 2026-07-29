using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;
using Xunit;

using Pica.Viewer.Services;
using Pica.Viewer.Tests.TestDoubles;

namespace Pica.Viewer.Tests.Services;

public sealed class WindowsFileActionsTests
{
    [Fact]
    public async Task GetOpenWithApplicationsAsync_WithImageExtension_ReturnsUniqueHandlers()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        WindowsApplicationIconLoader iconLoader = new(
            NullLogger<WindowsApplicationIconLoader>.Instance);
        RecordingFileRevealPlatform fileRevealPlatform = new();
        WindowsFileActions actions = new(iconLoader, fileRevealPlatform);

        IReadOnlyList<OpenWithApplication> applications =
            await actions.GetOpenWithApplicationsAsync(
                "image.png",
                CancellationToken.None);

        applications.Select(application => application.Identifier)
            .Should()
            .OnlyHaveUniqueItems();
        applications.Should().OnlyContain(application =>
            !string.IsNullOrWhiteSpace(application.Identifier)
                && !string.IsNullOrWhiteSpace(application.DisplayName));
    }

    [Theory]
    [InlineData(FileRevealWindowMode.ReuseExisting)]
    [InlineData(FileRevealWindowMode.OpenNew)]
    public async Task RevealInFolderAsync_DelegatesRequestedWindowMode(
        FileRevealWindowMode windowMode)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        const string filePath = "image.png";
        WindowsApplicationIconLoader iconLoader = new(
            NullLogger<WindowsApplicationIconLoader>.Instance);
        RecordingFileRevealPlatform fileRevealPlatform = new();
        WindowsFileActions actions = new(iconLoader, fileRevealPlatform);

        await actions.RevealInFolderAsync(
            filePath,
            windowMode,
            CancellationToken.None);

        fileRevealPlatform.CallCount.Should().Be(1);
        fileRevealPlatform.FilePath.Should().Be(filePath);
        fileRevealPlatform.WindowMode.Should().Be(windowMode);
    }
}
