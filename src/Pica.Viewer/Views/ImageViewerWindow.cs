using Microsoft.Extensions.Logging;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SukiUI.Controls;

using Pica.Protocol;
using Pica.Viewer.Controls;
using Pica.Viewer.Resources;
using Pica.Viewer.Services;
using Pica.Viewer.ViewModels;

namespace Pica.Viewer.Views;

public sealed partial class ImageViewerWindow : SukiWindow
{
    public Task CloseCleanupCompletion =>
        _closeCleanupCompletionSource.Task;

    internal ViewerWindowMode CurrentWindowMode => _windowMode.Mode;

    internal event EventHandler? ReadyForLoading;

    private const string AppIconAssetUri = "avares://Pica.Viewer/Assets/AppIcon.ico";
    private const double MinimumWindowWidth = 300d;
    private const double TitleLogoSize = 28d;

    private ImageViewerSessionViewModel Session =>
        _session
        ?? throw new InvalidOperationException(
            "The image viewer session has not been composed.");
    private ImageViewerWindowInteractionComposition Interaction =>
        _interaction
        ?? throw new InvalidOperationException(
            "The image viewer interactions have not been composed.");
    private Bitmap LogoBitmap =>
        _logoBitmap
        ?? throw new InvalidOperationException(
            "The image viewer logo has not been composed.");
    private ImageViewerView View =>
        _view
        ?? throw new InvalidOperationException(
            "The image viewer content has not been composed.");
    private ImageViewportController _viewport =>
        Interaction.Viewport;
    private ImageSelectionController _selection =>
        Interaction.Selection;
    private ViewerFloatingMenuController _floatingMenus =>
        Interaction.FloatingMenus;
    private ViewerChromeVisibilityController _chromeVisibility =>
        Interaction.ChromeVisibility;
    private ViewerCursorController _cursorController =>
        Interaction.Cursor;
    private ViewerSelectionInteractionController
        _selectionInteraction =>
            Interaction.SelectionInteraction;
    private ImageViewerActionController _actionController =>
        Interaction.Actions;
    private ViewerSettingsPanelController _settingsPanel =>
        Interaction.SettingsPanel;
    private ViewerWindowModeController _windowMode =>
        Interaction.WindowMode;
    private ViewerWindowResizeController _windowResize =>
        Interaction.WindowResize;
    private ViewerPointerInputController _pointerInput =>
        Interaction.PointerInput;
    private ViewerKeyboardInputController _keyboardInput =>
        Interaction.KeyboardInput;

    private readonly TaskCompletionSource _closeCleanupCompletionSource =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private ImageViewerSessionViewModel? _session;
    private ImageViewerWindowInteractionComposition? _interaction;
    private Bitmap? _logoBitmap;
    private ImageViewerView? _view;
    private ILogger<ImageViewerWindow>? _logger;
    private Exception? _closeVisualCleanupException;

    private ImageViewerWindow()
    {
    }

    public void CloseForApplicationExit()
    {
        Close();
    }

    internal static ImageViewerWindow Create(
        Func<
            ImageViewerWindow,
            AvaloniaUiFrameScheduler,
            ImageViewerWindowComposition> compositionFactory,
        ILogger<ImageViewerWindow> logger)
    {
        ArgumentNullException.ThrowIfNull(compositionFactory);
        ArgumentNullException.ThrowIfNull(logger);

        ImageViewerWindow window = new();
        window.Compose(compositionFactory, logger);

        return window;
    }

    internal void CompleteCloseCleanup()
    {
        _closeCleanupCompletionSource.TrySetResult();
    }

    internal void FailCloseCleanup(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        _closeCleanupCompletionSource.TrySetException(exception);
    }

    internal Exception? GetCloseVisualCleanupException()
    {
        return _closeVisualCleanupException;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        ReadyForLoading?.Invoke(this, EventArgs.Empty);
        _windowMode.ApplyInitialMode();
        View.FadeOverlay.Opacity = View.HiddenControlsOpacity;
        _cursorController.Start();
    }

    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        await Interaction.Close.HandleClosingAsync(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        ImageViewerWindowInteractionComposition? interaction = _interaction;
        ImageViewerView? view = _view;
        Bitmap? logoBitmap = _logoBitmap;
        List<Exception> cleanupFailures = [];

        _session = null;
        _interaction = null;
        _view = null;
        _logoBitmap = null;

        ExecuteCloseCleanup(
            () =>
            {
                interaction?.Viewport.StopScaleAnimation();
                interaction?.Viewport.StopPanMotion();
            },
            "stop viewer animations",
            cleanupFailures);
        ExecuteCloseCleanup(
            () => interaction?.Dispose(),
            "dispose viewer interaction controllers",
            cleanupFailures);
        ExecuteCloseCleanup(
            () => DetachVisualTree(view),
            "detach the viewer visual tree",
            cleanupFailures);
        ExecuteCloseCleanup(
            () => view?.Dispose(),
            "dispose the viewer content",
            cleanupFailures);
        ExecuteCloseCleanup(
            () => logoBitmap?.Dispose(),
            "dispose the viewer logo",
            cleanupFailures);
        _closeVisualCleanupException = cleanupFailures.Count switch
        {
            0 => null,
            1 => cleanupFailures[0],
            _ => new AggregateException(
                "Pica viewer visual cleanup failed.",
                cleanupFailures)
        };
        base.OnClosed(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if ((change.Property == WindowStateProperty)
            && (_interaction is not null))
        {
            _interaction.WindowMode.HandleWindowStateChanged();
        }
    }

    private static Bitmap LoadBitmap(string assetUri)
    {
        using Stream stream =
            AssetLoader.Open(new Uri(assetUri));

        return new Bitmap(stream);
    }

    private static WindowIcon LoadWindowIcon(string assetUri)
    {
        using Stream stream =
            AssetLoader.Open(new Uri(assetUri));

        return new WindowIcon(stream);
    }

    private void DetachVisualTree(ImageViewerView? view)
    {
        if (view is not null)
        {
            view.Image.Source = null;
            view.DataContext = null;
        }

        if (LogoContent is Image logo)
        {
            logo.Source = null;
        }

        Content = null;
        LogoContent = null;
        RightWindowTitleBarControls =
            new Avalonia.Controls.Controls();
        Hosts.Clear();
        Icon = null;
        DataContext = null;
    }

    private void ExecuteCloseCleanup(
        Action cleanup,
        string operationName,
        ICollection<Exception> failures)
    {
        ArgumentNullException.ThrowIfNull(cleanup);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        ArgumentNullException.ThrowIfNull(failures);

        try
        {
            cleanup();
        }
        catch (Exception ex)
        {
            failures.Add(ex);
            _logger?.LogError(
                ex,
                "Failed to {CleanupOperation} while closing the Pica viewer.",
                operationName);
        }
    }

    private void Compose(
        Func<
            ImageViewerWindow,
            AvaloniaUiFrameScheduler,
            ImageViewerWindowComposition> compositionFactory,
        ILogger<ImageViewerWindow> logger)
    {
        _logger = logger;
        AvaloniaUiFrameScheduler frameScheduler = new(this);
        ImageViewerWindowComposition? composition = null;
        Bitmap? logoBitmap = null;
        ImageViewerView? view = null;
        ImageViewerWindowInteractionComposition? interaction = null;

        try
        {
            composition = compositionFactory(this, frameScheduler);
            ImageViewerPresentationServices presentationServices =
                composition.PresentationServices;
            ImageViewerSettingsServices settingsServices =
                composition.SettingsServices;
            ImageViewerInteractionServices interactionServices =
                composition.InteractionServices;
            ImageViewerSettingsViewModel settings =
                settingsServices.Settings;
            ViewerWindowMode initialWindowMode =
                settings.RememberWindowPlacement
                && settings.IsWindowed
                ? ViewerWindowMode.Windowed
                : ViewerWindowMode.FullScreen;
            logoBitmap = LoadBitmap(AppIconAssetUri);
            ImageViewerViewEvents viewEvents = new()
            {
                ZoomOutClicked = OnZoomOutClicked,
                ResetClicked = OnResetClicked,
                ZoomInClicked = OnZoomInClicked,
                ToolMenuClicked = OnToolMenuClicked,
                CheckerboardBackgroundMenuClicked =
                    OnToolMenuActionClicked,
                FilteringMenuClicked = OnToolMenuActionClicked,
                ModeMenuClicked = OnModeMenuClicked,
                MainModeMenuClicked = OnToolMenuActionClicked,
                ChannelModeMenuClicked = OnToolMenuActionClicked,
                CloseClicked = OnCloseClicked,
                WindowModeClicked = OnWindowModeClicked,
                SettingsClicked = OnSettingsClicked,
                ContextCopyClicked = OnContextCopyClicked,
                ContextExternalActionClicked = OnContextExternalActionClicked,
                ContextSaveAsClicked = OnContextSaveAsClicked,
                ContextRevealInFolderClicked = OnContextRevealInFolderClicked,
                ContextOpenWithClicked = OnContextOpenWithClicked,
                ContextSelectAreaClicked = OnContextSelectAreaClicked,
                SelectionCopyClicked = OnSelectionCopyClicked,
                SelectionExternalActionClicked = OnSelectionExternalActionClicked,
                SelectionOpenWithClicked = OnSelectionOpenWithClicked,
                SelectionSaveAsClicked = OnSelectionSaveAsClicked,
                SelectionCancelClicked = OnSelectionCancelClicked,
                WindowResizePointerPressed = OnWindowResizePointerPressed,
                WindowResizePointerMoved = OnWindowResizePointerMoved,
                WindowResizePointerReleased = OnWindowResizePointerReleased
            };
            IReadOnlyList<ViewerSettingControl> settingControls =
                ViewerSettingsControlFactory.Create(settings);
            view = new ImageViewerView(
                composition.Session,
                settingsServices.ToolMenu,
                settingControls,
                initialWindowMode,
                viewEvents);
            interaction =
                ImageViewerWindowInteractionComposition.Create(
                    this,
                    view,
                    composition.Session,
                    settingsServices.Information,
                    OnOpenWithApplicationClicked,
                    OnChooseApplicationClicked,
                    Close,
                    logger,
                    interactionServices.Actions,
                    interactionServices.OpenWith,
                    presentationServices.Presentation,
                    presentationServices.Readiness,
                    frameScheduler,
                    settings,
                    composition.WindowPlacementProvider);
            _session = composition.Session;
            _logoBitmap = logoBitmap;
            _view = view;
            _interaction = interaction;
            View.ContextOpenWithButton.IsVisible =
                interactionServices.OpenWith.IsSupported;
            View.SelectionOpenWithButton.IsVisible =
                interactionServices.OpenWith.IsSupported;
            View.ApplyCheckerboardBackground(
                settings.IsCheckerboardBackgroundEnabled);
            View.ApplyImageFiltering(settings.IsFilteringEnabled);

            ConfigureWindow();
            Interaction.Presentation.ApplyInformation();
            AttachEvents();
        }
        catch (Exception)
        {
            interaction?.Dispose();
            view?.Dispose();
            logoBitmap?.Dispose();
            composition?.Dispose();
            frameScheduler.Dispose();
            throw;
        }
    }

    private void ConfigureWindow()
    {
        Background = Brushes.Black;
        CanResize = false;
        CanFullScreen = true;
        CanPin = true;
        Cursor = ViewerCursors.Arrow;
        Icon = LoadWindowIcon(AppIconAssetUri);
        IsMenuVisible = false;
        IsTitleBarVisible = _windowMode.IsWindowed;
        LogoContent = new Image
        {
            Width = TitleLogoSize,
            Height = TitleLogoSize,
            Source = LogoBitmap,
            Stretch = Stretch.Uniform
        };
        MinWidth = MinimumWindowWidth;
        RightWindowTitleBarControls = View.TitleBarSettingsControls;
        ShowBottomBorder = false;
        ShowInTaskbar = true;
        ShowTitlebarBackground = _windowMode.IsWindowed;
        Title = PicaProtocolConstants.ApplicationName;
        TitleFontWeight = FontWeight.Normal;
        TitleBarAnimationEnabled = false;
        TitleBarVisibilityOnFullScreen = TitleBarVisibilityMode.Hidden;
        WindowState = _windowMode.IsWindowed
            ? WindowState.Normal
            : WindowState.FullScreen;
        Content = View;
        _windowMode.ApplyConfiguredGeometry();
    }

    private void AttachEvents()
    {
        View.ViewerArea.PointerPressed +=
            _pointerInput.OnPointerPressed;
        View.ViewerArea.PointerMoved +=
            _pointerInput.OnPointerMoved;
        View.ViewerArea.PointerReleased +=
            _pointerInput.OnPointerReleased;
        View.ViewerArea.PointerWheelChanged +=
            _pointerInput.OnPointerWheelChanged;
        View.ViewerArea.SizeChanged += OnViewerAreaSizeChanged;
        AddHandler(
            KeyDownEvent,
            _keyboardInput.OnPreviewKeyDown,
            RoutingStrategies.Tunnel);
        KeyDown += _keyboardInput.OnKeyDown;
        KeyUp += _keyboardInput.OnKeyUp;
        PositionChanged += OnWindowPositionChanged;
        Resized += OnWindowResized;
        View.LeftNavigationArea.PointerPressed += OnLeftNavigationPressed;
        View.RightNavigationArea.PointerPressed += OnRightNavigationPressed;
        View.ViewerContextMenu.PointerPressed += OnFloatingMenuPointerPressed;
        View.ContextOpenWithButton.PointerEntered +=
            _floatingMenus.OnContextOpenWithAnchorPointerEntered;
        View.ContextOpenWithButton.PointerExited +=
            _floatingMenus.OnSubmenuAnchorPointerExited;
        View.SelectionOpenWithButton.PointerExited +=
            _floatingMenus.OnSubmenuAnchorPointerExited;
        View.OpenWithMenu.PointerEntered +=
            _floatingMenus.OnSubmenuPointerEntered;
        View.OpenWithMenu.PointerExited +=
            _floatingMenus.OnSubmenuPointerExited;
        View.OpenWithMenu.PointerPressed += OnFloatingMenuPointerPressed;
        View.ToolMenu.PointerPressed += OnFloatingMenuPointerPressed;
        View.ModeMenuButton.PointerEntered +=
            _floatingMenus.OnModeMenuAnchorPointerEntered;
        View.ModeMenuButton.PointerExited +=
            _floatingMenus.OnSubmenuAnchorPointerExited;
        View.ModeMenu.PointerEntered +=
            _floatingMenus.OnSubmenuPointerEntered;
        View.ModeMenu.PointerExited +=
            _floatingMenus.OnSubmenuPointerExited;
        View.ModeMenu.PointerPressed += OnFloatingMenuPointerPressed;
        View.SelectionToolbar.PointerPressed += OnFloatingMenuPointerPressed;
        View.SettingsPanel.PointerPressed += OnFloatingMenuPointerPressed;
        View.Root.PointerExited +=
            _pointerInput.OnRootPointerExited;
    }
}
