namespace Pica.Desktop.Services;

internal sealed class PicaDesktopState
{
    public bool IsClipboardShortcutEnabled { get; set; }
    public bool IsFullscreenClipboardShortcutEnabled { get; set; }
    public PicaClipboardShortcutGesture ClipboardShortcut { get; set; } = PicaClipboardShortcutGesture.Default;

    public int BackgroundIdleTimeoutSeconds { get; set; } =
        PicaBackgroundIdleTimeoutSettings.DefaultTimeoutSeconds;

    internal bool RequiresClipboardAgent => IsClipboardShortcutEnabled && IsFullscreenClipboardShortcutEnabled;

    internal PicaDesktopState CreateCopy()
    {
        return (PicaDesktopState)MemberwiseClone();
    }

    internal PicaDesktopState CreateNormalizedCopy()
    {
        PicaDesktopState normalizedState = CreateCopy();
        normalizedState.ClipboardShortcut = ClipboardShortcut is { IsSupported: true }
            ? ClipboardShortcut : PicaClipboardShortcutGesture.Default;
        normalizedState.BackgroundIdleTimeoutSeconds =
            PicaBackgroundIdleTimeoutSettings.Normalize(
                BackgroundIdleTimeoutSeconds);

        return normalizedState;
    }
}
