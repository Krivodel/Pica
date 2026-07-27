using Pica.Viewer.Services;

namespace Pica.Viewer.Controls;

internal static class ViewerSettingChoices
{
    public static IReadOnlyList<ViewerSettingOption<int>> SpeedOptions { get; } =
        ViewerSettingsDefaults.SpeedValues
            .Select(speed => new ViewerSettingOption<int>(speed, $"x{speed}"))
            .ToList();
    public static IReadOnlyList<ViewerSettingOption<WindowResizeBehavior>> ResizeBehaviorOptions { get; } =
        new List<ViewerSettingOption<WindowResizeBehavior>>
        {
            new(WindowResizeBehavior.Free, "Свободный размер"),
            new(WindowResizeBehavior.FitWhenWindowed, "Подгонять при переходе в окно"),
            new(WindowResizeBehavior.AlwaysFitImage, "Всегда подгонять под изображение")
        };
}
