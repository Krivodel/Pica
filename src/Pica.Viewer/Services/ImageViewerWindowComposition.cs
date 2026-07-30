using Microsoft.Extensions.Logging;

using Pica.Viewer.ViewModels;
using Pica.Viewer.Views;

namespace Pica.Viewer.Services;

internal sealed class ImageViewerWindowComposition : IDisposable
{
    internal ImageViewerSessionViewModel Session { get; }
    internal ImageViewerPresentationServices PresentationServices { get; }
    internal ImageViewerSettingsServices SettingsServices { get; }
    internal ImageViewerInteractionServices InteractionServices { get; }
    internal ViewerWindowPlacementProvider WindowPlacementProvider { get; }
    internal IReadOnlyList<ViewerSettingContribution>
        SettingContributions { get; }

    private readonly ImageViewerWindowLifetime _lifetime;

    internal ImageViewerWindowComposition(
        ImageViewerWindow window,
        AvaloniaUiFrameScheduler frameScheduler,
        ImageViewerSessionViewModel session,
        ImageViewerPresentationServices presentationServices,
        ImageViewerSettingsServices settingsServices,
        ImageViewerInteractionServices interactionServices,
        ViewerWindowPlacementProvider windowPlacementProvider,
        IReadOnlyList<ViewerSettingContribution> settingContributions,
        ILogger<ImageViewerWindowLifetime> lifetimeLogger)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(frameScheduler);
        Session = session ?? throw new ArgumentNullException(nameof(session));
        PresentationServices = presentationServices
            ?? throw new ArgumentNullException(nameof(presentationServices));
        SettingsServices = settingsServices
            ?? throw new ArgumentNullException(nameof(settingsServices));
        InteractionServices = interactionServices
            ?? throw new ArgumentNullException(nameof(interactionServices));
        WindowPlacementProvider = windowPlacementProvider
            ?? throw new ArgumentNullException(nameof(windowPlacementProvider));
        SettingContributions = settingContributions
            ?? throw new ArgumentNullException(nameof(settingContributions));
        _lifetime = new ImageViewerWindowLifetime(
            window,
            frameScheduler,
            session,
            presentationServices,
            settingsServices,
            interactionServices,
            lifetimeLogger);
    }

    public void Dispose()
    {
        _lifetime.Dispose();
    }
}
