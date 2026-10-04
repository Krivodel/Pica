using Pica.Viewer.Resources;
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
            new ViewerSettingOption<WindowResizeBehavior>(WindowResizeBehavior.Free,
                ViewerLocalization.Get(PicaViewerLocalizationKeys.ResizeFree))
            { LocalizationKey = PicaViewerLocalizationKeys.ResizeFree },
            new ViewerSettingOption<WindowResizeBehavior>(WindowResizeBehavior.FitWhenWindowed,
                ViewerLocalization.Get(PicaViewerLocalizationKeys.ResizeWhenWindowed))
            { LocalizationKey = PicaViewerLocalizationKeys.ResizeWhenWindowed },
            new ViewerSettingOption<WindowResizeBehavior>(WindowResizeBehavior.AlwaysFitImage,
                ViewerLocalization.Get(PicaViewerLocalizationKeys.ResizeAlwaysFit))
            { LocalizationKey = PicaViewerLocalizationKeys.ResizeAlwaysFit }
        };
}
