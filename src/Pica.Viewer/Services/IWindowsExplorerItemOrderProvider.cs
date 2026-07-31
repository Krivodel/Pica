namespace Pica.Viewer.Services;

public interface IWindowsExplorerItemOrderProvider
{
    IReadOnlyList<string>? GetItemPaths(
        string directoryPath,
        long sourceWindowHandle);
}
