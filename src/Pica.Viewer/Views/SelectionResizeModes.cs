namespace Pica.Viewer.Views;

[Flags]
internal enum SelectionResizeModes
{
    None = 0,
    Left = 1,
    Right = 2,
    Top = 4,
    Bottom = 8,
    TopLeft = Top | Left,
    TopRight = Top | Right,
    BottomRight = Bottom | Right,
    BottomLeft = Bottom | Left
}
