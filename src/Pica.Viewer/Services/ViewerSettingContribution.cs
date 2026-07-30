using Pica.Viewer.Controls;

namespace Pica.Viewer.Services;

public abstract class ViewerSettingContribution
{
    public string Label { get; }

    private protected ViewerSettingContribution(string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        Label = label;
    }

    internal abstract ViewerSettingControl CreateControl();
}
