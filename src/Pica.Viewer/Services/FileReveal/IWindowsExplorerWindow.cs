namespace Pica.Viewer.Services.FileReveal;

internal interface IWindowsExplorerWindow : IDisposable
{
    void SelectFile(string fileName);
}
