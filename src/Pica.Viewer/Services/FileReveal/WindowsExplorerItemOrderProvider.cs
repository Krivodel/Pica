using Microsoft.Extensions.Logging;

namespace Pica.Viewer.Services.FileReveal;

internal sealed class WindowsExplorerItemOrderProvider
    : IWindowsExplorerItemOrderProvider
{
    private readonly IWindowsExplorerWindowLocator _windowLocator;
    private readonly ILogger<WindowsExplorerItemOrderProvider> _logger;

    public WindowsExplorerItemOrderProvider(
        IWindowsExplorerWindowLocator windowLocator,
        ILogger<WindowsExplorerItemOrderProvider> logger)
    {
        _windowLocator = windowLocator
            ?? throw new ArgumentNullException(nameof(windowLocator));
        _logger = logger
            ?? throw new ArgumentNullException(nameof(logger));
    }

    public IReadOnlyList<string>? GetItemPaths(
        string directoryPath,
        long sourceWindowHandle)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        ArgumentOutOfRangeException.ThrowIfZero(sourceWindowHandle);

        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            using IWindowsExplorerWindow? window =
                _windowLocator.FindByHandle(
                    directoryPath,
                    sourceWindowHandle);

            return window?.GetItemPathsInViewOrder();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to read the item order from the source File Explorer window.");

            return null;
        }
    }
}
