namespace Pica.Desktop.Services.Background;

internal static class PicaBackgroundActivationRouting
{
    public static bool RunsBeforeFrameworkInitialization =>
        OperatingSystem.IsWindows();
}
