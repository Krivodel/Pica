using Microsoft.Extensions.Logging;

namespace Pica.Viewer.Services;

internal static class PlatformFileActionsFactory
{
    public static IPlatformFileActions Create(
        ILogger<WindowsApplicationIconLoader> applicationIconLogger,
        IFileRevealPlatform fileRevealPlatform)
    {
        ArgumentNullException.ThrowIfNull(applicationIconLogger);
        ArgumentNullException.ThrowIfNull(fileRevealPlatform);

        return OperatingSystem.IsWindows()
            ? new WindowsFileActions(
                new WindowsApplicationIconLoader(applicationIconLogger),
                fileRevealPlatform)
            : new CrossPlatformFileActions(fileRevealPlatform);
    }
}
