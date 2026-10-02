namespace Pica.Desktop.Services;

internal sealed class WindowsShortcutRecordingSession
{
    internal bool IsComplete { get; private set; }
    internal bool IsCanceled { get; private set; }
    internal PicaClipboardShortcutGesture? Gesture { get; private set; }

    private const int EscapeKey = 0x1B;
    private readonly HashSet<int> _pressedKeys;

    internal WindowsShortcutRecordingSession(IReadOnlyCollection<int> pressedModifiers)
    {
        ArgumentNullException.ThrowIfNull(pressedModifiers);
        _pressedKeys = new HashSet<int>(pressedModifiers);
    }

    internal void ProcessKey(int virtualKey, bool isPressed, bool isExtended)
    {
        if (IsComplete || IsCanceled)
        {
            return;
        }

        if (!isPressed)
        {
            _pressedKeys.Remove(virtualKey);
            IsComplete = Gesture?.VirtualKey == virtualKey;
            return;
        }

        if (!_pressedKeys.Add(virtualKey))
        {
            return;
        }

        if (virtualKey == EscapeKey)
        {
            IsCanceled = true;
            return;
        }

        if ((Gesture is null) && (WindowsShortcutKeyCatalog.GetModifier(virtualKey) == PicaShortcutModifiers.None))
        {
            PicaShortcutModifiers modifiers = _pressedKeys.Aggregate(PicaShortcutModifiers.None,
                (current, key) => current | WindowsShortcutKeyCatalog.GetModifier(key));
            Gesture = new PicaClipboardShortcutGesture(virtualKey, modifiers, isExtended);
        }
    }
}
