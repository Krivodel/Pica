using Microsoft.Extensions.Logging;

using Avalonia.Interactivity;

using Pica.Viewer.Services;
using Pica.Viewer.ViewModels;

namespace Pica.Viewer.Views;

internal sealed class ImageViewerWindowInteractionComposition :
    IDisposable
{
    internal ViewerFrameAnimationRunner AnimationRunner { get; }
    internal ImageViewportController Viewport { get; }
    internal ImageSelectionController Selection { get; }
    internal ViewerCursorController Cursor { get; }
    internal ViewerFloatingMenuController FloatingMenus { get; }
    internal ViewerChromeVisibilityController ChromeVisibility { get; }
    internal ViewerSelectionInteractionController SelectionInteraction
    {
        get;
    }
    internal ViewerSettingsPanelController SettingsPanel { get; }
    internal ImageViewerActionController Actions { get; }
    internal ViewerWindowModeController WindowMode { get; }
    internal ViewerWindowResizeController WindowResize { get; }
    internal ViewerPointerInputController PointerInput { get; }
    internal ViewerKeyboardInputController KeyboardInput { get; }
    internal ViewerImagePresentationController Presentation { get; }
    internal ViewerWindowCloseController Close { get; }

    private readonly ViewerSettingsChangeController _settingsChanges;

    private ImageViewerWindowInteractionComposition(
        ViewerFrameAnimationRunner animationRunner,
        ImageViewportController viewport,
        ImageSelectionController selection,
        ViewerCursorController cursor,
        ViewerFloatingMenuController floatingMenus,
        ViewerChromeVisibilityController chromeVisibility,
        ViewerSelectionInteractionController selectionInteraction,
        ViewerSettingsPanelController settingsPanel,
        ImageViewerActionController actions,
        ViewerWindowModeController windowMode,
        ViewerWindowResizeController windowResize,
        ViewerPointerInputController pointerInput,
        ViewerKeyboardInputController keyboardInput,
        ViewerSettingsChangeController settingsChanges,
        ViewerImagePresentationController presentation,
        ViewerWindowCloseController close)
    {
        AnimationRunner = animationRunner;
        Viewport = viewport;
        Selection = selection;
        Cursor = cursor;
        FloatingMenus = floatingMenus;
        ChromeVisibility = chromeVisibility;
        SelectionInteraction = selectionInteraction;
        SettingsPanel = settingsPanel;
        Actions = actions;
        WindowMode = windowMode;
        WindowResize = windowResize;
        PointerInput = pointerInput;
        KeyboardInput = keyboardInput;
        _settingsChanges = settingsChanges;
        Presentation = presentation;
        Close = close;
    }

    public void Dispose()
    {
        Presentation.Dispose();
        _settingsChanges.Dispose();
        Cursor.Dispose();
        FloatingMenus.Dispose();
        Selection.Dispose();
        WindowMode.Dispose();
    }

    internal static ImageViewerWindowInteractionComposition Create(
        ImageViewerWindow owner,
        ImageViewerView view,
        ImageViewerSessionViewModel session,
        ImageViewerInformationViewModel information,
        EventHandler<RoutedEventArgs> openWithApplicationClicked,
        EventHandler<RoutedEventArgs> chooseApplicationClicked,
        Action close,
        ILogger<ImageViewerWindow> logger,
        ImageViewerActionsViewModel actions,
        ImageViewerOpenWithViewModel openWith,
        ImagePresentationController imagePresentation,
        IImagePresentationReadiness presentationReadiness,
        IUiFrameScheduler animationFrameScheduler,
        ImageViewerSettingsViewModel settings,
        ViewerWindowPlacementProvider windowPlacementProvider)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(information);
        ArgumentNullException.ThrowIfNull(openWithApplicationClicked);
        ArgumentNullException.ThrowIfNull(chooseApplicationClicked);
        ArgumentNullException.ThrowIfNull(close);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(openWith);
        ArgumentNullException.ThrowIfNull(imagePresentation);
        ArgumentNullException.ThrowIfNull(presentationReadiness);
        ArgumentNullException.ThrowIfNull(animationFrameScheduler);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(windowPlacementProvider);
        ViewerFrameAnimationRunner animationRunner = new(
            animationFrameScheduler);
        ImageViewportController viewport = new(
            owner,
            view,
            imagePresentation,
            settings,
            animationFrameScheduler,
            animationRunner);
        ImageSelectionController selection = new(
            owner,
            view,
            actions,
            imagePresentation,
            viewport,
            animationRunner);
        ViewerCursorController cursor = new(
            owner,
            view,
            selection,
            viewport);
        ViewerFloatingMenuController floatingMenus = new(
            view,
            openWith,
            imagePresentation,
            viewport,
            selection,
            openWithApplicationClicked,
            chooseApplicationClicked);
        ViewerChromeVisibilityController chromeVisibility = new(
            view,
            viewport,
            selection,
            floatingMenus);
        ViewerSelectionInteractionController
            selectionInteraction = new(
                selection,
                cursor,
                chromeVisibility,
                floatingMenus);
        ViewerSettingsPanelController settingsPanel = new(
            view,
            animationRunner);
        ImageViewerActionController actionController = new(
            owner,
            view,
            actions,
            openWith,
            imagePresentation,
            presentationReadiness,
            selection,
            animationRunner,
            selectionInteraction.Cancel,
            floatingMenus.HideOpenWithAfterAction);
        ViewerWindowGeometryController windowGeometry = new(
            owner,
            view);
        ViewerWindowPlacementController windowPlacement = new(
            owner,
            settings,
            windowPlacementProvider,
            viewport,
            windowGeometry);
        ViewerWindowModeController windowMode = new(
            owner,
            view,
            settings,
            windowPlacement,
            viewport,
            animationFrameScheduler,
            settingsPanel.HideImmediately);
        ViewerWindowResizeController windowResize = new(
            owner,
            view,
            settings,
            viewport,
            windowMode,
            windowGeometry);
        ViewerPointerInputController pointerInput = new(
            view,
            settings,
            viewport,
            selection,
            selectionInteraction,
            floatingMenus,
            chromeVisibility,
            cursor,
            windowMode);
        ViewerKeyboardInputController keyboardInput = new(
            view,
            session,
            settings,
            viewport,
            selection,
            selectionInteraction,
            actionController,
            chromeVisibility,
            settingsPanel,
            pointerInput,
            close);
        ViewerSettingsChangeController settingsChanges = new(
            view,
            settings,
            viewport,
            pointerInput,
            windowMode);
        ViewerImagePresentationController presentation = new(
            owner,
            view,
            information,
            imagePresentation,
            settings,
            viewport,
            selection,
            selectionInteraction,
            windowMode);
        ViewerWindowCloseController closeController = new(
            owner,
            settings,
            windowMode,
            logger);

        return new ImageViewerWindowInteractionComposition(
            animationRunner,
            viewport,
            selection,
            cursor,
            floatingMenus,
            chromeVisibility,
            selectionInteraction,
            settingsPanel,
            actionController,
            windowMode,
            windowResize,
            pointerInput,
            keyboardInput,
            settingsChanges,
            presentation,
            closeController);
    }
}
