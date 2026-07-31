using Pica.Viewer.Services.FileReveal;

namespace Pica.Viewer.Tests.TestDoubles;

internal sealed class StubWindowsExplorerWindow
    : IWindowsExplorerWindow
{
    private readonly IReadOnlyList<string> _itemPaths;
    private readonly Exception? _readException;

    internal StubWindowsExplorerWindow(
        IReadOnlyList<string> itemPaths,
        Exception? readException = null)
    {
        _itemPaths = itemPaths
            ?? throw new ArgumentNullException(nameof(itemPaths));
        _readException = readException;
    }

    public IReadOnlyList<string> GetItemPathsInViewOrder()
    {
        if (_readException is not null)
        {
            throw _readException;
        }

        return _itemPaths;
    }

    public void SelectFile(string fileName)
    {
        _ = fileName;
    }

    public void Dispose()
    {
    }
}
