namespace Pica.Viewer.ViewModels;

internal sealed class ImageViewerToolMenuViewModel
{
    public ImageViewerSessionViewModel Session { get; }
    public ImageViewerSettingsViewModel Settings { get; }

    internal ImageViewerToolMenuViewModel(
        ImageViewerSessionViewModel session,
        ImageViewerSettingsViewModel settings)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
        Settings = settings
            ?? throw new ArgumentNullException(nameof(settings));
    }
}
