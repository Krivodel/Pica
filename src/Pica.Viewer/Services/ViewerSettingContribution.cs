using Pica.Viewer.Controls;

namespace Pica.Viewer.Services;

public abstract class ViewerSettingContribution
{
    public string Label { get; }
    public string? LocalizationKey { get; init; }
    public ViewerSettingPlacement Placement { get; init; }

    private protected ViewerSettingContribution(
        string label,
        ViewerSettingPlacement placement = ViewerSettingPlacement.Inline)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        Label = label;
        Placement = placement;
    }

    internal ViewerSettingControl CreateLocalizedControl()
    {
        ViewerSettingControl control = CreateControl();

        if (LocalizationKey is not null)
        {
            control.ApplyLocalization(LocalizationKey);
        }

        return control;
    }

    internal abstract ViewerSettingControl CreateControl();
}
