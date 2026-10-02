using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace Pica.Viewer.Controls;

internal sealed class ViewerSettingsPanel : Border
{
    private const double MaximumPanelWidth = 360d;
    private const double MinimumPanelWidth = 280d;
    private const double ControlSpacing = 8d;
    private const double SectionSpacing = 14d;

    internal ViewerSettingsPanel(IReadOnlyList<ViewerSettingControl> settingControls)
    {
        ArgumentNullException.ThrowIfNull(settingControls);

        MinWidth = MinimumPanelWidth;
        MaxWidth = MaximumPanelWidth;
        Padding = new Thickness(16d);
        Classes.Add("modal-glass-panel");
        Child = new ScrollViewer
        {
            Content = CreateContent(settingControls, true),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
    }

    internal static StackPanel CreateContent(IReadOnlyList<ViewerSettingControl> settingControls, bool includeTitle)
    {
        StackPanel content = new()
        {
            Spacing = SectionSpacing
        };
        if (includeTitle)
        {
            content.Children.Add(new TextBlock
            {
                FontSize = 17d,
                FontWeight = FontWeight.SemiBold,
                Text = "Настройки"
            });
        }

        foreach (ViewerSettingControl settingControl in settingControls)
        {
            Control control = settingControl.Label is { } label
                ? CreateLabeledControl(label, settingControl.Control)
                : settingControl.Control;
            content.Children.Add(control);
        }

        return content;
    }

    private static StackPanel CreateLabeledControl(string label, Control control)
    {
        StackPanel container = new()
        {
            Spacing = ControlSpacing
        };
        container.Children.Add(new TextBlock
        {
            Text = label
        });
        container.Children.Add(control);

        return container;
    }
}
