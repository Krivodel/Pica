using System.Reflection;
using System.Runtime.InteropServices;

using Microsoft.Extensions.Logging;

using Pica.Viewer.Services;

namespace Pica.Viewer.Services.FileReveal;

internal sealed class WindowsFileRevealHandler
{
    private const int NewWindowActivationAttemptCount = 20;
    private static readonly TimeSpan NewWindowActivationRetryDelay =
        TimeSpan.FromMilliseconds(50d);

    private readonly IWindowsExplorerWindowLocator _windowLocator;
    private readonly IStandardFileRevealer _standardFileRevealer;
    private readonly ILogger<WindowsFileRevealHandler> _logger;

    public WindowsFileRevealHandler(
        IWindowsExplorerWindowLocator windowLocator,
        IStandardFileRevealer standardFileRevealer,
        ILogger<WindowsFileRevealHandler> logger)
    {
        _windowLocator = windowLocator
            ?? throw new ArgumentNullException(nameof(windowLocator));
        _standardFileRevealer = standardFileRevealer
            ?? throw new ArgumentNullException(nameof(standardFileRevealer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task RevealAsync(
        string filePath,
        FileRevealWindowMode windowMode,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ct.ThrowIfCancellationRequested();

        if (windowMode == FileRevealWindowMode.ReuseExisting
            && TryRevealInWindow(filePath))
        {
            return;
        }

        IReadOnlySet<long> existingWindowHandles =
            _windowLocator.GetWindowHandles();
        _standardFileRevealer.Reveal(filePath);
        await ActivateNewWindowAsync(
                filePath,
                existingWindowHandles,
                ct)
            .ConfigureAwait(false);
    }

    private async Task ActivateNewWindowAsync(
        string filePath,
        IReadOnlySet<long> existingWindowHandles,
        CancellationToken ct)
    {
        for (int attempt = 0;
             attempt < NewWindowActivationAttemptCount;
             attempt++)
        {
            if (TryRevealInWindow(filePath, existingWindowHandles))
            {
                return;
            }

            if (attempt < NewWindowActivationAttemptCount - 1)
            {
                await Task.Delay(NewWindowActivationRetryDelay, ct)
                    .ConfigureAwait(false);
            }
        }
    }

    private bool TryRevealInWindow(
        string filePath,
        IReadOnlySet<long>? excludedWindowHandles = null)
    {
        string directoryPath = Path.GetDirectoryName(filePath)
            ?? throw new InvalidOperationException(
                "The image directory could not be determined.");
        using IWindowsExplorerWindow? window =
            _windowLocator.Find(directoryPath, excludedWindowHandles);

        if (window is null)
        {
            return false;
        }

        try
        {
            window.SelectFile(Path.GetFileName(filePath));

            return true;
        }
        catch (Exception ex) when (IsSelectionFailure(ex))
        {
            _logger.LogDebug(
                ex,
                "Failed to select a file in an existing File Explorer window.");

            return false;
        }
    }

    private static bool IsSelectionFailure(Exception exception)
    {
        return exception is COMException
            or InvalidCastException
            or InvalidOperationException
            or MissingMemberException
            or TargetInvocationException;
    }
}
