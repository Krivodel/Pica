namespace Pica.Viewer.Services.FileReveal;

internal interface IWindowsExplorerWindowLocator
{
    IReadOnlySet<long> GetWindowHandles();

    IWindowsExplorerWindow? Find(
        string directoryPath,
        IReadOnlySet<long>? excludedWindowHandles = null);

    IWindowsExplorerWindow? FindByHandle(
        string directoryPath,
        long windowHandle);
}
