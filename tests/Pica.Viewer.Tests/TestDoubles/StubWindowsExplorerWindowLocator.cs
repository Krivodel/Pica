using Pica.Viewer.Services.FileReveal;

namespace Pica.Viewer.Tests.TestDoubles;

internal sealed class StubWindowsExplorerWindowLocator
    : IWindowsExplorerWindowLocator
{
    public string? DirectoryPath { get; private set; }
    public long? WindowHandle { get; private set; }
    public int FindByHandleCallCount { get; private set; }

    private readonly IWindowsExplorerWindow? _window;

    internal StubWindowsExplorerWindowLocator(
        IWindowsExplorerWindow? window)
    {
        _window = window;
    }

    public IReadOnlySet<long> GetWindowHandles()
    {
        return new HashSet<long>();
    }

    public IWindowsExplorerWindow? Find(
        string directoryPath,
        IReadOnlySet<long>? excludedWindowHandles = null)
    {
        _ = directoryPath;
        _ = excludedWindowHandles;

        return null;
    }

    public IWindowsExplorerWindow? FindByHandle(
        string directoryPath,
        long windowHandle)
    {
        DirectoryPath = directoryPath;
        WindowHandle = windowHandle;
        FindByHandleCallCount++;

        return _window;
    }
}
