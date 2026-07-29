using Pica.Viewer.Controls;
using Pica.Viewer.Services;
using Pica.Viewer.ViewModels;

namespace Pica.Viewer.Views;

internal static class ViewerSettingsControlFactory
{
    private const double ImageInformationSettingsTopSpacing = 10d;

    internal static IReadOnlyList<ViewerSettingControl> Create(
        ImageViewerSettingsViewModel settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        List<ViewerSettingControl> settingControls =
        [
            new ViewerChoiceSettingControl<int>(
                "Скорость перемещения",
                ViewerSettingChoices.SpeedOptions,
                settings.MovementSpeed,
                settings.ChangeMovementSpeedCommand),
            new ViewerCheckBoxSettingControl(
                "Инерция перемещения",
                settings.IsPanningInertiaEnabled,
                settings.ChangePanningInertiaCommand),
            new ViewerChoiceSettingControl<int>(
                "Скорость масштабирования",
                ViewerSettingChoices.SpeedOptions,
                settings.ZoomSpeed,
                settings.ChangeZoomSpeedCommand),
            new ViewerCheckBoxSettingControl(
                "Свободное отдаление",
                settings.AllowFreeZoomOut,
                settings.ChangeAllowFreeZoomOutCommand),
            new ViewerChoiceSettingControl<WindowResizeBehavior>(
                "Изменение размера окна",
                ViewerSettingChoices.ResizeBehaviorOptions,
                settings.ResizeBehavior,
                settings.ChangeResizeBehaviorCommand),
            new ViewerCheckBoxSettingControl(
                "Разворачивать двойным щелчком",
                settings.ExpandOnDoubleClick,
                settings.ChangeExpandOnDoubleClickCommand),
            new ViewerCheckBoxSettingControl(
                "Запоминать положение и размер окна",
                settings.RememberWindowPlacement,
                settings.ChangeRememberWindowPlacementCommand),
            new ViewerCheckBoxSettingControl(
                "Быстрая загрузка",
                settings.IsFastLoadingEnabled,
                settings.ChangeFastLoadingCommand),
            new ViewerCheckBoxSettingControl(
                "Показывать название",
                settings.ShowImageName,
                settings.ChangeShowImageNameCommand,
                topSpacing: ImageInformationSettingsTopSpacing),
            new ViewerCheckBoxSettingControl(
                "Показывать формат",
                settings.ShowImageFormat,
                settings.ChangeShowImageFormatCommand),
            new ViewerCheckBoxSettingControl(
                "Показывать дату изменения",
                settings.ShowImageModificationDate,
                settings.ChangeShowImageModificationDateCommand),
            new ViewerCheckBoxSettingControl(
                "Показывать разрешение",
                settings.ShowImageResolution,
                settings.ChangeShowImageResolutionCommand)
        ];

        return settingControls;
    }
}
