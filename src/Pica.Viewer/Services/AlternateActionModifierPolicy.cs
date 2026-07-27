using Avalonia.Input;

namespace Pica.Viewer.Services;

public static class AlternateActionModifierPolicy
{
    public static bool IsActive(KeyModifiers modifiers)
    {
        KeyModifiers alternateActionModifiers = KeyModifiers.Shift
            | KeyModifiers.Control
            | KeyModifiers.Alt;

        return (modifiers & alternateActionModifiers) != KeyModifiers.None;
    }
}
