namespace Pica.Viewer.Services.FileReveal;

internal interface IWindowsExplorerWindow : IDisposable
{
    IReadOnlyList<string> GetItemPathsInViewOrder();

    void SelectFile(string fileName);
}
