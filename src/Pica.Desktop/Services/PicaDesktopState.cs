namespace Pica.Desktop.Services;

internal sealed class PicaDesktopState
{
    public int BackgroundIdleTimeoutSeconds { get; set; } =
        PicaBackgroundIdleTimeoutSettings.DefaultTimeoutSeconds;

    internal PicaDesktopState CreateCopy()
    {
        return (PicaDesktopState)MemberwiseClone();
    }

    internal PicaDesktopState CreateNormalizedCopy()
    {
        PicaDesktopState normalizedState = CreateCopy();
        normalizedState.BackgroundIdleTimeoutSeconds =
            PicaBackgroundIdleTimeoutSettings.Normalize(
                BackgroundIdleTimeoutSeconds);

        return normalizedState;
    }
}
