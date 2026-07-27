using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;
using Xunit;

using Pica.Viewer.Services;
using Pica.Viewer.Services.FileReveal;
using Pica.Viewer.Tests.TestDoubles;

namespace Pica.Viewer.Tests.Services.FileReveal;

public sealed class WindowsFileRevealHandlerTests
{
    private static readonly string FilePath = Path.GetFullPath(
        Path.Combine("Art", "image.png"));

    [Fact]
    public async Task RevealAsync_WithReusableWindow_SelectsFileWithoutOpeningNewWindow()
    {
        RecordingWindowsExplorerWindow window = new();
        RecordingWindowsExplorerWindowLocator locator = new(window, null);
        RecordingStandardFileRevealer standardRevealer = new();
        WindowsFileRevealHandler handler = CreateHandler(
            locator,
            standardRevealer);

        await handler.RevealAsync(
            FilePath,
            FileRevealWindowMode.ReuseExisting,
            CancellationToken.None);

        locator.DirectoryPath.Should().Be(Path.GetDirectoryName(FilePath));
        window.SelectedFileName.Should().Be(Path.GetFileName(FilePath));
        standardRevealer.CallCount.Should().Be(0);
        locator.GetWindowHandlesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task RevealAsync_WithoutReusableWindow_ActivatesNewWindow()
    {
        RecordingWindowsExplorerWindow newWindow = new();
        RecordingWindowsExplorerWindowLocator locator = new(
            null,
            newWindow);
        RecordingStandardFileRevealer standardRevealer = new();
        WindowsFileRevealHandler handler = CreateHandler(
            locator,
            standardRevealer);

        await handler.RevealAsync(
            FilePath,
            FileRevealWindowMode.ReuseExisting,
            CancellationToken.None);

        locator.CallCount.Should().Be(2);
        locator.GetWindowHandlesCallCount.Should().Be(1);
        standardRevealer.FilePath.Should().Be(FilePath);
        newWindow.SelectedFileName.Should().Be(Path.GetFileName(FilePath));
    }

    [Fact]
    public async Task RevealAsync_WithOpenNewMode_SkipsReusableWindowAndActivatesNewWindow()
    {
        RecordingWindowsExplorerWindow reusableWindow = new();
        RecordingWindowsExplorerWindow newWindow = new();
        RecordingWindowsExplorerWindowLocator locator = new(
            reusableWindow,
            newWindow);
        RecordingStandardFileRevealer standardRevealer = new();
        WindowsFileRevealHandler handler = CreateHandler(
            locator,
            standardRevealer);

        await handler.RevealAsync(
            FilePath,
            FileRevealWindowMode.OpenNew,
            CancellationToken.None);

        locator.CallCount.Should().Be(1);
        locator.GetWindowHandlesCallCount.Should().Be(1);
        reusableWindow.SelectedFileName.Should().BeNull();
        newWindow.SelectedFileName.Should().Be(Path.GetFileName(FilePath));
        standardRevealer.FilePath.Should().Be(FilePath);
    }

    private static WindowsFileRevealHandler CreateHandler(
        IWindowsExplorerWindowLocator locator,
        IStandardFileRevealer standardRevealer)
    {
        return new WindowsFileRevealHandler(
            locator,
            standardRevealer,
            NullLogger<WindowsFileRevealHandler>.Instance);
    }

    private sealed class RecordingWindowsExplorerWindowLocator
        : IWindowsExplorerWindowLocator
    {
        public string? DirectoryPath { get; private set; }
        public int CallCount { get; private set; }
        public int GetWindowHandlesCallCount { get; private set; }

        private readonly IWindowsExplorerWindow? _reusableWindow;
        private readonly IWindowsExplorerWindow? _newWindow;

        public RecordingWindowsExplorerWindowLocator(
            IWindowsExplorerWindow? reusableWindow,
            IWindowsExplorerWindow? newWindow)
        {
            _reusableWindow = reusableWindow;
            _newWindow = newWindow;
        }

        public IReadOnlySet<long> GetWindowHandles()
        {
            GetWindowHandlesCallCount++;

            return new HashSet<long> { 1L };
        }

        public IWindowsExplorerWindow? Find(
            string directoryPath,
            IReadOnlySet<long>? excludedWindowHandles = null)
        {
            CallCount++;
            DirectoryPath = directoryPath;

            return excludedWindowHandles is null
                ? _reusableWindow
                : _newWindow;
        }
    }

    private sealed class RecordingWindowsExplorerWindow
        : IWindowsExplorerWindow
    {
        public string? SelectedFileName { get; private set; }

        public void SelectFile(string fileName)
        {
            SelectedFileName = fileName;
        }

        public void Dispose()
        {
        }
    }
}
