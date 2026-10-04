namespace Pica.Desktop.Services;

internal sealed class PicaDesktopState
{
    public string? LocalizationId { get; set; }
    public bool IsClipboardShortcutEnabled { get; set; }
    public bool IsFullscreenClipboardShortcutEnabled { get; set; }
    public PicaClipboardShortcutGesture ClipboardShortcut { get; set; } = PicaClipboardShortcutGesture.Default;
    public bool HasSeenFileAssociationsPrompt { get; set; }
    public bool HasSeenClipboardShortcutPrompt { get; set; }
    public Dictionary<string, string?> PreviousFileAssociations { get; set; } =
        new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    public int BackgroundIdleTimeoutSeconds { get; set; } =
        PicaBackgroundIdleTimeoutSettings.DefaultTimeoutSeconds;

    internal bool RequiresClipboardAgent => IsClipboardShortcutEnabled && IsFullscreenClipboardShortcutEnabled;

    internal PicaDesktopState CreateCopy()
    {
        PicaDesktopState copy = (PicaDesktopState)MemberwiseClone();
        copy.PreviousFileAssociations = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (KeyValuePair<string, string?> association in PreviousFileAssociations ?? new Dictionary<string, string?>())
        {
            copy.PreviousFileAssociations[association.Key] = association.Value;
        }

        return copy;
    }

    internal PicaDesktopState CreateNormalizedCopy()
    {
        PicaDesktopState normalizedState = CreateCopy();
        normalizedState.HasSeenClipboardShortcutPrompt |= IsClipboardShortcutEnabled;
        normalizedState.ClipboardShortcut = ClipboardShortcut is { IsSupported: true }
            ? ClipboardShortcut : PicaClipboardShortcutGesture.Default;
        normalizedState.BackgroundIdleTimeoutSeconds =
            PicaBackgroundIdleTimeoutSettings.Normalize(
                BackgroundIdleTimeoutSeconds);

        return normalizedState;
    }
}
