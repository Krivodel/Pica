namespace Pica.Desktop.Services;

[Flags]
internal enum PicaShortcutModifiers
{
    None = 0,
    Shift = 1,
    Control = 2,
    Alt = 4,
    Windows = 8
}
