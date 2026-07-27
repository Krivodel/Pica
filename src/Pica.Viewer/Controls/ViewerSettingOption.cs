namespace Pica.Viewer.Controls;

internal sealed record ViewerSettingOption<TValue>(TValue Value, string DisplayName)
{
    public override string ToString()
    {
        return DisplayName;
    }
}
