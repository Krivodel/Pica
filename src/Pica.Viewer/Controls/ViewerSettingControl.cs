using Avalonia.Controls;

namespace Pica.Viewer.Controls;

internal abstract class ViewerSettingControl
{
    internal string? Label { get; }
    internal abstract Control Control { get; }

    protected ViewerSettingControl(string? label)
    {
        Label = label;
    }
}
