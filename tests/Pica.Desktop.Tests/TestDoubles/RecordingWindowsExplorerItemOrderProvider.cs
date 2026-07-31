using Pica.Viewer.Services;

namespace Pica.Desktop.Tests.TestDoubles;

internal sealed class RecordingWindowsExplorerItemOrderProvider
    : IWindowsExplorerItemOrderProvider
{
    public string? DirectoryPath { get; private set; }
    public long? SourceWindowHandle { get; private set; }
    public int CallCount { get; private set; }

    private readonly IReadOnlyList<string>? _itemPaths;

    internal RecordingWindowsExplorerItemOrderProvider(
        IReadOnlyList<string>? itemPaths)
    {
        _itemPaths = itemPaths;
    }

    public IReadOnlyList<string>? GetItemPaths(
        string directoryPath,
        long sourceWindowHandle)
    {
        DirectoryPath = directoryPath;
        SourceWindowHandle = sourceWindowHandle;
        CallCount++;

        return _itemPaths;
    }
}
