using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Pica.Viewer.Services;
using Pica.Viewer.Services.FileReveal;
using Pica.Viewer.ViewModels;

namespace Pica.Viewer;

public static class DependencyInjection
{
    public static IServiceCollection AddPicaViewer(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ImageFormatRegistry>();
        services.AddSingleton<IImageFormatRegistry>(provider =>
            provider.GetRequiredService<ImageFormatRegistry>());
        services.AddSingleton<IImageDecoderResolver>(provider =>
            provider.GetRequiredService<ImageFormatRegistry>());
        services.AddSingleton<IImageViewerStateService, ImageViewerStateService>();
        services.AddSingleton<IViewModelErrorHandler, ViewModelErrorHandler>();
        services.AddSingleton<
            IImageFileMetadataProvider,
            ImageFileMetadataProvider>();
        services.AddSingleton<ImagePreviewLoader>();
        services.AddSingleton<FullResolutionImageLoader>();
        services.AddSingleton<IViewerUiDispatcher, AvaloniaViewerUiDispatcher>();
        services.AddSingleton<IImageChannelBitmapLoader, ImageChannelBitmapLoader>();
        services.AddSingleton<PngImageEncoder>();
        services.AddSingleton<ClipboardImagePreparer>();
        services.AddSingleton<IStandardFileRevealer, StandardFileRevealer>();
        services.AddSingleton<IFileRevealProcessLauncher, FileRevealProcessLauncher>();
        services.AddSingleton<
            IWindowsExplorerWindowLocator,
            WindowsExplorerWindowLocator>();
        services.AddSingleton<WindowsFileRevealHandler>();
        services.AddSingleton<MacOsFileRevealHandler>();
        services.AddSingleton<LinuxFileRevealHandler>();
        services.AddSingleton<IFileRevealPlatform, FileRevealPlatform>();
        services.AddSingleton<IPlatformFileActions>(provider =>
            PlatformFileActionsFactory.Create(
                provider.GetRequiredService<ILogger<WindowsApplicationIconLoader>>(),
                provider.GetRequiredService<IFileRevealPlatform>()));
        services.AddSingleton<ClipboardFlushCoordinator>();
        services.AddSingleton<IClipboardImageWriter>(provider =>
            provider.GetRequiredService<ClipboardFlushCoordinator>());
        services.AddSingleton<ViewerClipboardFactory>();
        services.AddSingleton<ImageViewerPresentationFactory>();
        services.AddSingleton<ImageViewerSettingsFactory>();
        services.AddSingleton<ImageViewerInteractionFactory>();
        services.AddSingleton<ImageViewerWindowComposer>();
        services.AddSingleton<IImageViewerWindowFactory, ImageViewerWindowFactory>();

        return services;
    }
}
