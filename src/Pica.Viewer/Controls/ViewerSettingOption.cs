namespace Pica.Viewer.Controls;

internal sealed record ViewerSettingOption<TValue>(TValue Value, string DisplayName)
{
    public string? LocalizationKey { get; init; }

    public override string ToString()
    {
        return DisplayName;
    }
}
