using System.Globalization;
using System.Runtime.Versioning;

using Microsoft.Extensions.Logging;

namespace Pica.Viewer.Services.FileReveal;

internal sealed class WindowsExplorerWindowLocator
    : IWindowsExplorerWindowLocator
{
    private const string ShellApplicationProgrammaticId = "Shell.Application";

    private readonly ILogger<WindowsExplorerWindowLocator> _logger;

    public WindowsExplorerWindowLocator(
        ILogger<WindowsExplorerWindowLocator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public IReadOnlySet<long> GetWindowHandles()
    {
        HashSet<long> windowHandles = [];

        if (!OperatingSystem.IsWindows())
        {
            return windowHandles;
        }

        object? shellApplication = null;
        object? shellWindows = null;

        try
        {
            if (!TryOpenShellWindows(
                    out shellApplication,
                    out shellWindows,
                    out int windowCount))
            {
                return windowHandles;
            }

            object activeShellWindows = shellWindows
                ?? throw new InvalidOperationException(
                    "The File Explorer window collection is unavailable.");

            for (int index = 0; index < windowCount; index++)
            {
                TryAddWindowHandle(
                    activeShellWindows,
                    index,
                    windowHandles);
            }
        }
        catch (Exception ex) when (WindowsShellAutomation.IsAutomationException(ex))
        {
            _logger.LogDebug(
                ex,
                "Failed to inspect existing File Explorer windows.");
        }
        finally
        {
            WindowsShellAutomation.Release(shellWindows);
            WindowsShellAutomation.Release(shellApplication);
        }

        return windowHandles;
    }

    public IWindowsExplorerWindow? Find(
        string directoryPath,
        IReadOnlySet<long>? excludedWindowHandles = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        object? shellApplication = null;
        object? shellWindows = null;

        try
        {
            if (!TryOpenShellWindows(
                    out shellApplication,
                    out shellWindows,
                    out int windowCount))
            {
                return null;
            }

            object activeShellWindows = shellWindows
                ?? throw new InvalidOperationException(
                    "The File Explorer window collection is unavailable.");

            for (int index = 0; index < windowCount; index++)
            {
                IWindowsExplorerWindow? window = GetMatchingWindow(
                    activeShellWindows,
                    index,
                    directoryPath,
                    excludedWindowHandles);

                if (window is not null)
                {
                    return window;
                }
            }
        }
        catch (Exception ex) when (WindowsShellAutomation.IsAutomationException(ex))
        {
            _logger.LogDebug(
                ex,
                "Failed to inspect existing File Explorer windows.");
        }
        finally
        {
            WindowsShellAutomation.Release(shellWindows);
            WindowsShellAutomation.Release(shellApplication);
        }

        return null;
    }

    private IWindowsExplorerWindow? GetMatchingWindow(
        object shellWindows,
        int index,
        string directoryPath,
        IReadOnlySet<long>? excludedWindowHandles)
    {
        object? window = null;
        object? document = null;
        object? folder = null;
        object? folderSelf = null;

        try
        {
            window = WindowsShellAutomation.InvokeMethod(
                shellWindows,
                "Item",
                [index]);

            if (window is null)
            {
                return null;
            }

            long windowHandle = WindowsShellAutomation.GetWindowHandle(window);

            if (excludedWindowHandles?.Contains(windowHandle) == true)
            {
                return null;
            }

            document = WindowsShellAutomation.GetProperty(window, "Document");
            folder = document is null
                ? null
                : WindowsShellAutomation.GetProperty(document, "Folder");

            if (document is null || folder is null)
            {
                return null;
            }

            folderSelf = WindowsShellAutomation.GetProperty(folder, "Self");
            string? openDirectoryPath = folderSelf is null
                ? null
                : WindowsShellAutomation.GetProperty(folderSelf, "Path") as string;

            if (!AreSameDirectory(openDirectoryPath, directoryPath))
            {
                return null;
            }

            WindowsExplorerWindow result = new(window, document, folder);
            window = null;
            document = null;
            folder = null;

            return result;
        }
        catch (Exception ex) when (IsWindowInspectionFailure(ex))
        {
            _logger.LogDebug(
                ex,
                "Failed to inspect a Windows Explorer window.");

            return null;
        }
        finally
        {
            WindowsShellAutomation.Release(folderSelf);
            WindowsShellAutomation.Release(folder);
            WindowsShellAutomation.Release(document);
            WindowsShellAutomation.Release(window);
        }
    }

    [SupportedOSPlatform("windows")]
    private static bool TryOpenShellWindows(
        out object? shellApplication,
        out object? shellWindows,
        out int windowCount)
    {
        shellApplication = null;
        shellWindows = null;
        windowCount = 0;
        Type? shellApplicationType = Type.GetTypeFromProgID(
            ShellApplicationProgrammaticId);

        if (shellApplicationType is null)
        {
            return false;
        }

        shellApplication = Activator.CreateInstance(shellApplicationType);

        if (shellApplication is null)
        {
            return false;
        }

        shellWindows = WindowsShellAutomation.InvokeMethod(
            shellApplication,
            "Windows");

        if (shellWindows is null)
        {
            return false;
        }

        object? windowCountValue =
            WindowsShellAutomation.GetProperty(shellWindows, "Count");
        windowCount = Convert.ToInt32(
            windowCountValue,
            CultureInfo.InvariantCulture);

        return true;
    }

    private void TryAddWindowHandle(
        object shellWindows,
        int index,
        HashSet<long> windowHandles)
    {
        object? window = null;

        try
        {
            window = WindowsShellAutomation.InvokeMethod(
                shellWindows,
                "Item",
                [index]);

            if (window is not null)
            {
                windowHandles.Add(
                    WindowsShellAutomation.GetWindowHandle(window));
            }
        }
        catch (Exception ex) when (IsWindowInspectionFailure(ex))
        {
            _logger.LogDebug(
                ex,
                "Failed to inspect Windows Explorer window at index {WindowIndex}.",
                index);
        }
        finally
        {
            WindowsShellAutomation.Release(window);
        }
    }

    private static bool IsWindowInspectionFailure(Exception exception)
    {
        return WindowsShellAutomation.IsAutomationException(exception)
            || exception is ArgumentException
            or NotSupportedException;
    }

    private static bool AreSameDirectory(
        string? firstPath,
        string secondPath)
    {
        if (string.IsNullOrWhiteSpace(firstPath))
        {
            return false;
        }

        string normalizedFirstPath = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(firstPath));
        string normalizedSecondPath = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(secondPath));

        return string.Equals(
            normalizedFirstPath,
            normalizedSecondPath,
            StringComparison.OrdinalIgnoreCase);
    }
}
