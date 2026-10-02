using Avalonia.Input;

using Pica.Viewer.Views;

namespace Pica.Viewer.Services;

public static class ViewerKeyboardShortcutPolicy
{
    private const KeyModifiers TypingCommandModifiers = KeyModifiers.Control | KeyModifiers.Shift;

    public static bool ConflictsWithClipboard(Key key, PhysicalKey physicalKey, KeyModifiers modifiers)
    {
        return Resolve(key, physicalKey, modifiers) switch
        {
            ViewerKeyboardAction.None or ViewerKeyboardAction.Paste => false,
            ViewerKeyboardAction.Copy => (modifiers & ~TypingCommandModifiers) == KeyModifiers.None,
            ViewerKeyboardAction.ToggleFiltering or ViewerKeyboardAction.ToggleBackground => modifiers == KeyModifiers.None,
            _ => true
        };
    }

    internal static ViewerKeyboardAction Resolve(Key key, PhysicalKey physicalKey, KeyModifiers modifiers)
    {
        if ((key == Key.V) && ViewerInputModifiers.IsControlPressed(modifiers)
            && ((modifiers & ~TypingCommandModifiers) == KeyModifiers.None))
        {
            return ViewerKeyboardAction.Paste;
        }

        if ((key == Key.C) && ViewerInputModifiers.IsControlPressed(modifiers))
        {
            return ViewerKeyboardAction.Copy;
        }

        if (TryGetNavigationDirection(key, out _))
        {
            return ViewerKeyboardAction.Navigate;
        }

        if (TryGetFrameNavigationDirection(key, physicalKey, out _))
        {
            return ViewerKeyboardAction.NavigateFrame;
        }

        return key switch
        {
            Key.Escape => ViewerKeyboardAction.Escape,
            Key.Tab => ViewerKeyboardAction.ToggleImageMode,
            Key.F => ViewerKeyboardAction.ToggleFiltering,
            Key.T => ViewerKeyboardAction.ToggleBackground,
            Key.Space => ViewerKeyboardAction.ResetViewport,
            _ => ViewerKeyboardAction.None
        };
    }

    internal static bool TryGetNavigationDirection(Key key, out int direction)
    {
        direction = key switch
        {
            Key.Left or Key.A => -1,
            Key.Right or Key.D => 1,
            _ => 0
        };

        return direction != 0;
    }

    internal static bool TryGetFrameNavigationDirection(Key key, PhysicalKey physicalKey, out int direction)
    {
        direction = 0;

        if ((key == Key.OemComma) || (physicalKey == PhysicalKey.Comma))
        {
            direction = -1;
        }
        else if ((key == Key.OemPeriod) || (physicalKey == PhysicalKey.Period))
        {
            direction = 1;
        }

        return direction != 0;
    }
}
