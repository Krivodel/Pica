using Pica.Viewer.Services;

namespace Pica.Viewer.Services.FileReveal;

internal sealed class FileRevealPlatform : IFileRevealPlatform
{
    private readonly WindowsFileRevealHandler _windowsHandler;
    private readonly MacOsFileRevealHandler _macOsHandler;
    private readonly LinuxFileRevealHandler _linuxHandler;

    public FileRevealPlatform(
        WindowsFileRevealHandler windowsHandler,
        MacOsFileRevealHandler macOsHandler,
        LinuxFileRevealHandler linuxHandler)
    {
        _windowsHandler = windowsHandler
            ?? throw new ArgumentNullException(nameof(windowsHandler));
        _macOsHandler = macOsHandler
            ?? throw new ArgumentNullException(nameof(macOsHandler));
        _linuxHandler = linuxHandler
            ?? throw new ArgumentNullException(nameof(linuxHandler));
    }

    public Task RevealAsync(
        string filePath,
        FileRevealWindowMode windowMode,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ct.ThrowIfCancellationRequested();

        if (!Enum.IsDefined(windowMode))
        {
            throw new ArgumentOutOfRangeException(
                nameof(windowMode),
                windowMode,
                "Unsupported file reveal window mode.");
        }

        if (OperatingSystem.IsWindows())
        {
            return _windowsHandler.RevealAsync(
                filePath,
                windowMode,
                ct);
        }

        if (OperatingSystem.IsMacOS())
        {
            _macOsHandler.Reveal(filePath, windowMode);
        }
        else
        {
            _linuxHandler.Reveal(filePath, windowMode);
        }

        return Task.CompletedTask;
    }
}
