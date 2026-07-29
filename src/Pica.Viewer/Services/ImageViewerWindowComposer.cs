using Microsoft.Extensions.Logging;

using Pica.Protocol;
using Pica.Viewer.ViewModels;
using Pica.Viewer.Views;

namespace Pica.Viewer.Services;

internal sealed class ImageViewerWindowComposer
{
    private readonly ImageViewerPresentationFactory _presentationFactory;
    private readonly ImageViewerSettingsFactory _settingsFactory;
    private readonly ImageViewerInteractionFactory _interactionFactory;
    private readonly ILogger<ImageViewerWindow> _windowLogger;
    private readonly ILogger<ImageViewerWindowLifetime> _lifetimeLogger;

    public ImageViewerWindowComposer(
        ImageViewerPresentationFactory presentationFactory,
        ImageViewerSettingsFactory settingsFactory,
        ImageViewerInteractionFactory interactionFactory,
        ILogger<ImageViewerWindow> windowLogger,
        ILogger<ImageViewerWindowLifetime> lifetimeLogger)
    {
        _presentationFactory = presentationFactory
            ?? throw new ArgumentNullException(nameof(presentationFactory));
        _settingsFactory = settingsFactory
            ?? throw new ArgumentNullException(nameof(settingsFactory));
        _interactionFactory = interactionFactory
            ?? throw new ArgumentNullException(nameof(interactionFactory));
        _windowLogger = windowLogger
            ?? throw new ArgumentNullException(nameof(windowLogger));
        _lifetimeLogger = lifetimeLogger
            ?? throw new ArgumentNullException(nameof(lifetimeLogger));
    }

    internal ImageViewerWindow Create(
        PicaViewerRequest request,
        IViewerActionDispatcher actionDispatcher,
        ImageViewerState state)
    {
        ViewerAnimationFrameScheduler animationFrameScheduler = new();

        return Create(
            request,
            actionDispatcher,
            state,
            animationFrameScheduler);
    }

    internal ImageViewerWindow Create(
        PicaViewerRequest request,
        IViewerActionDispatcher actionDispatcher,
        ImageViewerState state,
        ViewerAnimationFrameScheduler animationFrameScheduler)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actionDispatcher);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(animationFrameScheduler);
        ImageViewerSession sessionState = new(
            request,
            state.IsFilteringEnabled);
        ImageViewerSessionViewModel session = new(sessionState);
        ImageViewerPresentationServices? presentationServices = null;
        ImageViewerSettingsServices? settingsServices = null;
        ImageViewerInteractionServices? interactionServices = null;

        try
        {
            presentationServices = _presentationFactory.Create(
                sessionState,
                animationFrameScheduler,
                state.IsFastLoadingEnabled);
            ViewerWindowPlacement initialPlacement = new(
                state.IsWindowed == true,
                state.WindowX,
                state.WindowY,
                state.WindowWidth,
                state.WindowHeight);
            ViewerWindowPlacementProvider windowPlacementProvider = new(
                initialPlacement);
            settingsServices = _settingsFactory.Create(
                sessionState,
                session,
                presentationServices.Presentation,
                presentationServices.LoadCoordinator,
                windowPlacementProvider,
                state);
            ViewerWindowPlatformContext platformContext = new();
            interactionServices = _interactionFactory.Create(
                sessionState,
                presentationServices.Presentation,
                presentationServices.Readiness,
                platformContext,
                actionDispatcher);
            ImageViewerWindow window = new(
                session,
                interactionServices.Actions,
                interactionServices.OpenWith,
                settingsServices.Information,
                settingsServices.ToolMenu,
                presentationServices.Presentation,
                presentationServices.Readiness,
                animationFrameScheduler,
                settingsServices.Settings,
                windowPlacementProvider,
                _windowLogger);
            platformContext.Initialize(window);
            _ = new ImageViewerWindowLifetime(
                window,
                session,
                presentationServices,
                settingsServices,
                interactionServices,
                _lifetimeLogger);

            return window;
        }
        catch (Exception)
        {
            interactionServices?.DisposeWithoutFlush();
            settingsServices?.Dispose();
            presentationServices?.Dispose();
            session.Dispose();
            throw;
        }
    }
}
