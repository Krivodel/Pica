using Pica.Viewer.Controls;

namespace Pica.Viewer.Services;

public abstract class ViewerSettingContribution
{
    public string Label { get; }
    public ViewerSettingPlacement Placement { get; }

    private protected ViewerSettingContribution(
        string label,
        ViewerSettingPlacement placement = ViewerSettingPlacement.Inline)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        Label = label;
        Placement = placement;
    }

    internal abstract ViewerSettingControl CreateControl();
}
