using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;
using Xunit;

using Pica.Viewer.Services.FileReveal;
using Pica.Viewer.Tests.TestDoubles;

namespace Pica.Viewer.Tests.Services.FileReveal;

public sealed class WindowsExplorerItemOrderProviderTests
{
    [Fact]
    public void GetItemPaths_WithMatchingWindow_ReturnsViewOrder()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string directoryPath = Path.GetFullPath("Images");
        string[] itemPaths =
        [
            Path.Combine(directoryPath, "02.png"),
            Path.Combine(directoryPath, "01.png")
        ];
        long sourceWindowHandle = 42L;
        StubWindowsExplorerWindow window = new(itemPaths);
        StubWindowsExplorerWindowLocator locator = new(window);
        WindowsExplorerItemOrderProvider provider = new(
            locator,
            NullLogger<WindowsExplorerItemOrderProvider>.Instance);

        IReadOnlyList<string>? result = provider.GetItemPaths(
            directoryPath,
            sourceWindowHandle);

        result.Should().Equal(itemPaths);
        locator.DirectoryPath.Should().Be(directoryPath);
        locator.WindowHandle.Should().Be(sourceWindowHandle);
        locator.FindByHandleCallCount.Should().Be(1);
    }

    [Fact]
    public void GetItemPaths_WhenViewReadFails_ReturnsNull()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string directoryPath = Path.GetFullPath("Images");
        InvalidOperationException readException = new(
            "The view is unavailable.");
        StubWindowsExplorerWindow window = new(
            Array.Empty<string>(),
            readException);
        StubWindowsExplorerWindowLocator locator = new(window);
        WindowsExplorerItemOrderProvider provider = new(
            locator,
            NullLogger<WindowsExplorerItemOrderProvider>.Instance);

        IReadOnlyList<string>? result = provider.GetItemPaths(
            directoryPath,
            42L);

        result.Should().BeNull();
    }
}
