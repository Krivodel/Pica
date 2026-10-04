using Pica.Viewer.Controls;
using Pica.Viewer.Resources;
using Pica.Viewer.Services;
using Pica.Viewer.ViewModels;

namespace Pica.Viewer.Views;

internal static class ViewerSettingsControlFactory
{
    private const double ImageInformationSettingsTopSpacing = 10d;

    internal static IReadOnlyList<ViewerSettingControl> Create(
        ImageViewerSettingsViewModel settings,
        IReadOnlyList<ViewerSettingContribution> settingContributions)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(settingContributions);

        List<ViewerSettingControl> settingControls =
        [
            new ViewerChoiceSettingControl<int>(
                ViewerLocalization.Get(PicaViewerLocalizationKeys.MovementSpeed),
                ViewerSettingChoices.SpeedOptions,
                settings.MovementSpeed,
                settings.ChangeMovementSpeedCommand).WithLocalization(PicaViewerLocalizationKeys.MovementSpeed),
            new ViewerCheckBoxSettingControl(
                ViewerLocalization.Get(PicaViewerLocalizationKeys.PanningInertia),
                settings.IsPanningInertiaEnabled,
                settings.ChangePanningInertiaCommand).WithLocalization(PicaViewerLocalizationKeys.PanningInertia),
            new ViewerChoiceSettingControl<int>(
                ViewerLocalization.Get(PicaViewerLocalizationKeys.ZoomSpeed),
                ViewerSettingChoices.SpeedOptions,
                settings.ZoomSpeed,
                settings.ChangeZoomSpeedCommand).WithLocalization(PicaViewerLocalizationKeys.ZoomSpeed),
            new ViewerCheckBoxSettingControl(
                ViewerLocalization.Get(PicaViewerLocalizationKeys.FreeZoomOut),
                settings.AllowFreeZoomOut,
                settings.ChangeAllowFreeZoomOutCommand).WithLocalization(PicaViewerLocalizationKeys.FreeZoomOut),
            new ViewerCheckBoxSettingControl(
                ViewerLocalization.Get(PicaViewerLocalizationKeys.PreserveView),
                settings.PreserveZoomAndPositionOnNavigation,
                settings.ChangePreserveZoomAndPositionOnNavigationCommand).WithLocalization(PicaViewerLocalizationKeys.PreserveView),
            new ViewerChoiceSettingControl<WindowResizeBehavior>(
                ViewerLocalization.Get(PicaViewerLocalizationKeys.ResizeBehavior),
                ViewerSettingChoices.ResizeBehaviorOptions,
                settings.ResizeBehavior,
                settings.ChangeResizeBehaviorCommand).WithLocalization(PicaViewerLocalizationKeys.ResizeBehavior),
            new ViewerCheckBoxSettingControl(
                ViewerLocalization.Get(PicaViewerLocalizationKeys.ExpandOnDoubleClick),
                settings.ExpandOnDoubleClick,
                settings.ChangeExpandOnDoubleClickCommand).WithLocalization(PicaViewerLocalizationKeys.ExpandOnDoubleClick),
            new ViewerCheckBoxSettingControl(
                ViewerLocalization.Get(PicaViewerLocalizationKeys.AutoHideTitleBar),
                settings.AutoHideWindowTitleBar,
                settings.ChangeAutoHideWindowTitleBarCommand,
                wrapContent: true).WithLocalization(PicaViewerLocalizationKeys.AutoHideTitleBar),
            new ViewerCheckBoxSettingControl(
                ViewerLocalization.Get(PicaViewerLocalizationKeys.RememberWindowPlacement),
                settings.RememberWindowPlacement,
                settings.ChangeRememberWindowPlacementCommand).WithLocalization(PicaViewerLocalizationKeys.RememberWindowPlacement)
        ];

        settingControls.InsertRange(0, settingContributions
            .Where(contribution => contribution.Placement == ViewerSettingPlacement.Header)
            .Select(contribution => contribution.CreateLocalizedControl()));
        settingControls.AddRange(settingContributions
            .Where(contribution => contribution.Placement == ViewerSettingPlacement.Inline)
            .Select(contribution => contribution.CreateLocalizedControl()));
        settingControls.AddRange(
        new List<ViewerSettingControl>
        {
            new ViewerCheckBoxSettingControl(
                ViewerLocalization.Get(PicaViewerLocalizationKeys.FastLoading),
                settings.IsFastLoadingEnabled,
                settings.ChangeFastLoadingCommand).WithLocalization(PicaViewerLocalizationKeys.FastLoading),
            new ViewerCheckBoxSettingControl(
                ViewerLocalization.Get(PicaViewerLocalizationKeys.ShowImageName),
                settings.ShowImageName,
                settings.ChangeShowImageNameCommand,
                topSpacing: ImageInformationSettingsTopSpacing).WithLocalization(PicaViewerLocalizationKeys.ShowImageName),
            new ViewerCheckBoxSettingControl(
                ViewerLocalization.Get(PicaViewerLocalizationKeys.ShowImageFormat),
                settings.ShowImageFormat,
                settings.ChangeShowImageFormatCommand).WithLocalization(PicaViewerLocalizationKeys.ShowImageFormat),
            new ViewerCheckBoxSettingControl(
                ViewerLocalization.Get(PicaViewerLocalizationKeys.ShowModificationDate),
                settings.ShowImageModificationDate,
                settings.ChangeShowImageModificationDateCommand).WithLocalization(PicaViewerLocalizationKeys.ShowModificationDate),
            new ViewerCheckBoxSettingControl(
                ViewerLocalization.Get(PicaViewerLocalizationKeys.ShowResolution),
                settings.ShowImageResolution,
                settings.ChangeShowImageResolutionCommand).WithLocalization(PicaViewerLocalizationKeys.ShowResolution)
        });
        settingControls.AddRange(settingContributions
            .Where(contribution => contribution.Placement == ViewerSettingPlacement.Footer)
            .Select(contribution => contribution.CreateLocalizedControl()));

        return settingControls;
    }
}
