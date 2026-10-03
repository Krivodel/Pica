using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Pica.Viewer.Controls;

internal sealed class ViewerSettingsPanel : Border
{
    private const double MaximumPanelWidth = 360d;
    private const double MinimumPanelWidth = 280d;

    internal ViewerSettingsPanel(IReadOnlyList<ViewerSettingControl> settingControls)
    {
        ArgumentNullException.ThrowIfNull(settingControls);

        MinWidth = MinimumPanelWidth;
        MaxWidth = MaximumPanelWidth;
        Padding = new Thickness(16d);
        Classes.Add("modal-glass-panel");
        Child = new ScrollViewer
        {
            Content = new ViewerSettingsContentControl(settingControls, true),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
    }
}
