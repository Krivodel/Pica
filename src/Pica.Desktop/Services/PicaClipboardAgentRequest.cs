namespace Pica.Desktop.Services;

internal sealed record PicaClipboardAgentRequest(
    PicaClipboardAgentOperation Operation,
    PicaClipboardShortcutGesture? Gesture = null,
    bool IsEnabled = false)
{
    internal void ApplyTo(PicaDesktopState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        switch (Operation)
        {
            case PicaClipboardAgentOperation.Initialize:
            case PicaClipboardAgentOperation.Validate:
                break;
            case PicaClipboardAgentOperation.SetGesture:
                state.ClipboardShortcut = Gesture ?? throw new InvalidDataException("No clipboard shortcut was supplied.");
                break;
            case PicaClipboardAgentOperation.SetEnabled:
                state.IsClipboardShortcutEnabled = IsEnabled;
                state.HasSeenClipboardShortcutPrompt |= IsEnabled;
                break;
            case PicaClipboardAgentOperation.SetFullscreenEnabled:
                state.IsFullscreenClipboardShortcutEnabled = IsEnabled;
                break;
            default:
                throw new InvalidDataException("The Pica clipboard agent command is unsupported.");
        }
    }
}
