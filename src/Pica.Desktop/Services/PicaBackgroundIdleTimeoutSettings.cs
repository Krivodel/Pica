using Pica.Desktop.Resources;

namespace Pica.Desktop.Services;

internal static class PicaBackgroundIdleTimeoutSettings
{
    internal const int DefaultTimeoutSeconds = 60;

    internal static IReadOnlyList<(
        int TimeoutSeconds,
        string LocalizationKey)> Options { get; } =
        new List<(int TimeoutSeconds, string LocalizationKey)>
        {
            (0, PicaDesktopLocalizationKeys.BackgroundNever),
            (15, PicaDesktopLocalizationKeys.Background15Seconds),
            (DefaultTimeoutSeconds, PicaDesktopLocalizationKeys.Background1Minute),
            (300, PicaDesktopLocalizationKeys.Background5Minutes)
        };

    internal static int Normalize(int timeoutSeconds)
    {
        return Options.Any(option =>
            option.TimeoutSeconds == timeoutSeconds)
            ? timeoutSeconds
            : DefaultTimeoutSeconds;
    }
}
