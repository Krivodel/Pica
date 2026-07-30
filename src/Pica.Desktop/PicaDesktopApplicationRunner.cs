using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace Pica.Desktop;

internal static class PicaDesktopApplicationRunner
{
    public static void Run(string[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (!OperatingSystem.IsWindows()
            || (Thread.CurrentThread.GetApartmentState()
                == ApartmentState.STA))
        {
            Start(arguments);
            return;
        }

        Thread applicationThread = new(() => Start(arguments));
        applicationThread.SetApartmentState(ApartmentState.STA);
        applicationThread.Start();
    }

    private static void Start(string[] arguments)
    {
        Program
            .BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(arguments);
    }
}
