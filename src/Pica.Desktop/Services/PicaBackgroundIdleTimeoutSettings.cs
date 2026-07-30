namespace Pica.Desktop.Services;

internal static class PicaBackgroundIdleTimeoutSettings
{
    internal const int DefaultTimeoutSeconds = 60;

    internal static IReadOnlyList<(
        int TimeoutSeconds,
        string DisplayName)> Options { get; } =
        new List<(int TimeoutSeconds, string DisplayName)>
        {
            (0, "Не оставаться в фоне"),
            (15, "15 секунд"),
            (DefaultTimeoutSeconds, "1 минута"),
            (300, "5 минут")
        };

    internal static int Normalize(int timeoutSeconds)
    {
        return Options.Any(option =>
            option.TimeoutSeconds == timeoutSeconds)
            ? timeoutSeconds
            : DefaultTimeoutSeconds;
    }
}
