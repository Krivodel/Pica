namespace Pica.Viewer.Services;

public sealed record ViewerSettingChoice<TValue>(
    TValue Value,
    string DisplayName);
