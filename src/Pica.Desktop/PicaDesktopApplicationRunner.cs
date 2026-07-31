using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

using Pica.Desktop.Services;

namespace Pica.Desktop;

internal static class PicaDesktopApplicationRunner
{
    public static void Run(
        string[] arguments,
        PicaLaunchContext launchContext)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(launchContext);

        if (!OperatingSystem.IsWindows()
            || (Thread.CurrentThread.GetApartmentState()
                == ApartmentState.STA))
        {
            Start(arguments, launchContext);
            return;
        }

        Thread applicationThread = new(
            () => Start(arguments, launchContext));
        applicationThread.SetApartmentState(ApartmentState.STA);
        applicationThread.Start();
    }

    private static void Start(
        string[] arguments,
        PicaLaunchContext launchContext)
    {
        Program
            .BuildAvaloniaApp(launchContext)
            .StartWithClassicDesktopLifetime(arguments);
    }
}
