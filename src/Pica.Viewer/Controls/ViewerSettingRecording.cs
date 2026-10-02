using Avalonia;
using Avalonia.Controls;

namespace Pica.Viewer.Controls;

internal sealed class ViewerSettingRecording : AvaloniaObject
{
    internal static readonly AttachedProperty<bool> IsActiveProperty =
        AvaloniaProperty.RegisterAttached<ViewerSettingRecording, TopLevel, bool>("IsActive");

    internal static bool IsActive(TopLevel? owner)
    {
        return owner?.GetValue(IsActiveProperty) == true;
    }
}
