using Avalonia.Controls;

using Krivodeling.Localization.Avalonia;

namespace Pica.Viewer.Controls;

internal abstract class ViewerSettingControl
{
    internal string? Label { get; }
    internal string? LabelLocalizationKey { get; private set; }
    internal abstract Control Control { get; }
    internal virtual Task Completion => Task.CompletedTask;

    protected const double ErrorSpacing = 4d;

    protected ViewerSettingControl(string? label)
    {
        Label = label;
    }

    internal virtual void ApplyLocalization(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        LabelLocalizationKey = key;
    }

    internal ViewerSettingControl WithLocalization(string key)
    {
        ApplyLocalization(key);

        return this;
    }

    internal virtual void RefreshValue()
    {
    }
}
