namespace Pica.Desktop.Services;

internal sealed record PicaClipboardAgentReply(PicaDesktopState State, PicaShortcutFailure? Error = null);
