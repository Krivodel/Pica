using System.ComponentModel;

using Microsoft.Extensions.Logging;

using Pica.Viewer.Services;

namespace Pica.Viewer.Services.FileReveal;

internal sealed class LinuxFileRevealHandler
{
    private const string DbusSendExecutableName = "dbus-send";

    private readonly IStandardFileRevealer _standardFileRevealer;
    private readonly IFileRevealProcessLauncher _processLauncher;
    private readonly ILogger<LinuxFileRevealHandler> _logger;

    public LinuxFileRevealHandler(
        IStandardFileRevealer standardFileRevealer,
        IFileRevealProcessLauncher processLauncher,
        ILogger<LinuxFileRevealHandler> logger)
    {
        _standardFileRevealer = standardFileRevealer
            ?? throw new ArgumentNullException(nameof(standardFileRevealer));
        _processLauncher = processLauncher
            ?? throw new ArgumentNullException(nameof(processLauncher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void Reveal(string filePath, FileRevealWindowMode windowMode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (windowMode == FileRevealWindowMode.OpenNew)
        {
            _standardFileRevealer.Reveal(filePath);
            return;
        }

        try
        {
            _processLauncher.Start(
                DbusSendExecutableName,
                CreateShowItemsArguments(filePath));
        }
        catch (Win32Exception ex)
        {
            _logger.LogDebug(
                ex,
                "The desktop file-manager interface is unavailable; using the default opener.");
            _standardFileRevealer.Reveal(filePath);
        }
    }

    private static IReadOnlyList<string> CreateShowItemsArguments(
        string filePath)
    {
        UriBuilder fileUriBuilder = new()
        {
            Scheme = Uri.UriSchemeFile,
            Host = string.Empty,
            Path = filePath
        };

        return new List<string>
        {
            "--session",
            "--dest=org.freedesktop.FileManager1",
            "--type=method_call",
            "/org/freedesktop/FileManager1",
            "org.freedesktop.FileManager1.ShowItems",
            $"array:string:{fileUriBuilder.Uri.AbsoluteUri}",
            "string:"
        };
    }
}
