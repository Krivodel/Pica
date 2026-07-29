using Avalonia.Input;

namespace Pica.Viewer.Views;

internal static class ViewerInputModifiers
{
    internal static bool IsControlPressed(
        KeyModifiers modifiers)
    {
        return IsAnyPressed(modifiers, KeyModifiers.Control);
    }

    internal static bool IsControlKey(Key key)
    {
        return (key == Key.LeftCtrl)
            || (key == Key.RightCtrl);
    }

    internal static bool IsBaseZoomSpeedRequested(
        KeyModifiers modifiers)
    {
        return IsAnyPressed(
            modifiers,
            KeyModifiers.Shift
                | KeyModifiers.Alt
                | KeyModifiers.Control);
    }

    private static bool IsAnyPressed(
        KeyModifiers modifiers,
        KeyModifiers requestedModifiers)
    {
        return (modifiers & requestedModifiers)
            != KeyModifiers.None;
    }
}
