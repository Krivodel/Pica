using Avalonia.Input;
using Avalonia.Win32.Input;

namespace Pica.Desktop.Services;

internal static class WindowsShortcutKeyCatalog
{
    internal static IReadOnlyList<int> PhysicalModifiers { get; } = Array.AsReadOnly(new Key[]
    {
        Key.LeftShift, Key.RightShift, Key.LeftCtrl, Key.RightCtrl,
        Key.LeftAlt, Key.RightAlt, Key.LWin, Key.RWin
    }.Select(KeyInterop.VirtualKeyFromKey).ToArray());

    private const int KeyPressedMask = 0x8000;

    internal static IReadOnlyCollection<int> ReadPressedModifiers()
    {
        return PhysicalModifiers.Where(key => (WindowsShortcutNative.GetAsyncKeyState(key) & KeyPressedMask) != 0).ToArray();
    }

    internal static PicaShortcutModifiers GetModifier(int virtualKey)
    {
        return KeyInterop.KeyFromVirtualKey(virtualKey, 0) switch
        {
            Key.LeftShift or Key.RightShift => PicaShortcutModifiers.Shift,
            Key.LeftCtrl or Key.RightCtrl => PicaShortcutModifiers.Control,
            Key.LeftAlt or Key.RightAlt => PicaShortcutModifiers.Alt,
            Key.LWin or Key.RWin => PicaShortcutModifiers.Windows,
            _ => PicaShortcutModifiers.None
        };
    }
}
