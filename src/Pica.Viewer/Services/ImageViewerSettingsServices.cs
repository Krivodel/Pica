using Pica.Viewer.ViewModels;

namespace Pica.Viewer.Services;

internal sealed class ImageViewerSettingsServices : IDisposable
{
    internal ImageViewerSettingsViewModel Settings { get; }
    internal ImageViewerInformationViewModel Information { get; }
    internal ImageViewerToolMenuViewModel ToolMenu { get; }

    internal ImageViewerSettingsServices(
        ImageViewerSettingsViewModel settings,
        ImageViewerInformationViewModel information,
        ImageViewerToolMenuViewModel toolMenu)
    {
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        Information = information
            ?? throw new ArgumentNullException(nameof(information));
        ToolMenu = toolMenu ?? throw new ArgumentNullException(nameof(toolMenu));
    }

    public void Dispose()
    {
        Information.Dispose();
        Settings.Dispose();
    }
}
