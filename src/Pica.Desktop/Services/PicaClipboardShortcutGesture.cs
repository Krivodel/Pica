using Avalonia.Input;
using Avalonia.Win32.Input;

using Pica.Viewer.Services;

namespace Pica.Desktop.Services;

internal sealed record PicaClipboardShortcutGesture(
    int VirtualKey,
    PicaShortcutModifiers Modifiers,
    bool IsExtended = false) : IViewerClipboardShortcut
{
    internal static PicaClipboardShortcutGesture Default { get; } =
        new(VirtualKeyV, PicaShortcutModifiers.Control | PicaShortcutModifiers.Shift);
    internal uint RegistrationModifiers =>
        ((Modifiers & PicaShortcutModifiers.Alt) != 0 ? RegistrationAlt : 0)
        | ((Modifiers & PicaShortcutModifiers.Control) != 0 ? RegistrationControl : 0)
        | ((Modifiers & PicaShortcutModifiers.Shift) != 0 ? RegistrationShift : 0);
    internal int KeyData => IsExtended ? ExtendedKeyData : 0;
    internal short ShellHotKey => (short)(VirtualKey | (((int)Modifiers | (IsExtended ? ShellExtendedKey : 0)) << 8));
    internal Key Key => KeyInterop.KeyFromVirtualKey(VirtualKey, KeyData);
    internal bool IsSupported
    {
        get
        {
            bool hasTypingModifier = (Modifiers & (PicaShortcutModifiers.Control | PicaShortcutModifiers.Alt)) != 0;
            bool isFunctionKey = (VirtualKey >= FirstFunctionKey) && (VirtualKey <= LastFunctionKey);

            return (VirtualKey > 0) && (VirtualKey <= byte.MaxValue)
                && ((Modifiers & ~(PicaShortcutModifiers.Control | PicaShortcutModifiers.Alt | PicaShortcutModifiers.Shift)) == 0)
                && (hasTypingModifier || (isFunctionKey && (Modifiers == PicaShortcutModifiers.None)))
                && (VirtualKey is not (EscapeKey or TabKey or SpaceKey or ReservedFunctionKey or PacketKey))
                && !((VirtualKey == F4Key) && ((Modifiers & PicaShortcutModifiers.Alt) != 0)
                    && ((Modifiers & PicaShortcutModifiers.Control) == 0))
                && !((VirtualKey == DeleteKey) && ((Modifiers & (PicaShortcutModifiers.Control | PicaShortcutModifiers.Alt))
                    == (PicaShortcutModifiers.Control | PicaShortcutModifiers.Alt)))
                && (Key != Key.None)
                && (WindowsShortcutKeyCatalog.GetModifier(VirtualKey) == PicaShortcutModifiers.None);
        }
    }

    private const int VirtualKeyV = 0x56;
    private const int FirstFunctionKey = 0x70;
    private const int LastFunctionKey = 0x87;
    private const int ReservedFunctionKey = 0x7B;
    private const int EscapeKey = 0x1B;
    private const int TabKey = 0x09;
    private const int SpaceKey = 0x20;
    private const int DeleteKey = 0x2E;
    private const int F4Key = 0x73;
    private const int PacketKey = 0xE7;
    private const int ExtendedKeyData = 1 << 24;
    private const int ShellExtendedKey = 8;
    private const uint RegistrationAlt = 1;
    private const uint RegistrationControl = 2;
    private const uint RegistrationShift = 4;

    public bool Matches(Key key, PhysicalKey physicalKey, KeyModifiers modifiers)
    {
        return (key == Key) && (modifiers == ToKeyModifiers());
    }

    internal void Validate()
    {
        if (!IsSupported)
        {
            throw new PicaShortcutException(PicaShortcutFailure.Unsupported);
        }
    }

    internal bool HasSameRegistration(PicaClipboardShortcutGesture? other)
    {
        return (VirtualKey == other?.VirtualKey) && (Modifiers == other.Modifiers);
    }

    internal string Format()
    {
        return new KeyGesture(Key, ToKeyModifiers()).ToString();
    }

    internal KeyModifiers ToKeyModifiers()
    {
        KeyModifiers result = KeyModifiers.None;

        if ((Modifiers & PicaShortcutModifiers.Control) != 0)
        {
            result |= KeyModifiers.Control;
        }

        if ((Modifiers & PicaShortcutModifiers.Alt) != 0)
        {
            result |= KeyModifiers.Alt;
        }

        if ((Modifiers & PicaShortcutModifiers.Shift) != 0)
        {
            result |= KeyModifiers.Shift;
        }

        return result;
    }
}
