using Pica.Viewer.Services;

namespace Pica.Viewer.Controls;

internal static class ViewerSettingChoices
{
    public static IReadOnlyList<ViewerSettingOption<int>> BackgroundIdleTimeoutOptions { get; } =
        new List<ViewerSettingOption<int>>
        {
            new(0, "Не оставаться в фоне"),
            new(15, "15 секунд"),
            new(60, "1 минута"),
            new(300, "5 минут")
        };
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
