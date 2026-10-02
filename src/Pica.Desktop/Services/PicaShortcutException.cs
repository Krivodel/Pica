namespace Pica.Desktop.Services;

internal sealed class PicaShortcutException : Exception
{
    internal PicaShortcutFailure Failure { get; }

    internal PicaShortcutException(PicaShortcutFailure failure)
        : base($"Pica clipboard shortcut validation failed: {failure}.")
    {
        Failure = failure;
    }
}
