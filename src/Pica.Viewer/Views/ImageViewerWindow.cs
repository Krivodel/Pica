using Microsoft.Extensions.Logging;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
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
    internal ViewerWindowMode CurrentWindowMode =>
        _windowMode.Mode;

    internal event EventHandler? ReadyForLoading;

    private const string AppIconAssetUri = "avares://Pica.Viewer/Assets/AppIcon.ico";
    private const double MinimumWindowWidth = 300d;
    private const double TitleLogoSize = 28d;

    private ImageViewportController _viewport =>
        _interaction.Viewport;
    private ImageSelectionController _selection =>
        _interaction.Selection;
    private ViewerFloatingMenuController _floatingMenus =>
        _interaction.FloatingMenus;
    private ViewerChromeVisibilityController _chromeVisibility =>
        _interaction.ChromeVisibility;
    private ViewerCursorController _cursorController =>
        _interaction.Cursor;
    private ViewerSelectionInteractionController
        _selectionInteraction =>
            _interaction.SelectionInteraction;
    private ImageViewerActionController _actionController =>
        _interaction.Actions;
    private ViewerSettingsPanelController _settingsPanel =>
        _interaction.SettingsPanel;
    private ViewerWindowModeController _windowMode =>
        _interaction.WindowMode;
    private ViewerWindowResizeController _windowResize =>
        _interaction.WindowResize;
    private ViewerPointerInputController _pointerInput =>
        _interaction.PointerInput;
    private ViewerKeyboardInputController _keyboardInput =>
        _interaction.KeyboardInput;

    private readonly ImageViewerSessionViewModel _session;
    private readonly ViewerAnimationFrameScheduler _animationFrameScheduler;
    private readonly ImageViewerWindowInteractionComposition
        _interaction;
    private readonly Bitmap _logoBitmap;
    private readonly ImageViewerView _view;
    internal ImageViewerWindow(
        ImageViewerSessionViewModel session,
        ImageViewerActionsViewModel actions,
        ImageViewerOpenWithViewModel openWith,
        ImageViewerInformationViewModel information,
        ImageViewerToolMenuViewModel toolMenu,
        ImagePresentationController imagePresentation,
        IImagePresentationReadiness presentationReadiness,
        ViewerAnimationFrameScheduler animationFrameScheduler,
        ImageViewerSettingsViewModel settings,
        ViewerWindowPlacementProvider windowPlacementProvider,
        ILogger<ImageViewerWindow> logger)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(openWith);
        ArgumentNullException.ThrowIfNull(information);
        ArgumentNullException.ThrowIfNull(toolMenu);
        ArgumentNullException.ThrowIfNull(imagePresentation);
        ArgumentNullException.ThrowIfNull(presentationReadiness);
        _animationFrameScheduler = animationFrameScheduler
            ?? throw new ArgumentNullException(nameof(animationFrameScheduler));
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(windowPlacementProvider);
        ArgumentNullException.ThrowIfNull(logger);
        ViewerWindowMode initialWindowMode =
            settings.RememberWindowPlacement
            && settings.IsWindowed
            ? ViewerWindowMode.Windowed
            : ViewerWindowMode.FullScreen;
        _logoBitmap = LoadBitmap(AppIconAssetUri);
        ImageViewerViewEvents viewEvents = new()
        {
            ZoomOutClicked = OnZoomOutClicked,
            ResetClicked = OnResetClicked,
            ZoomInClicked = OnZoomInClicked,
            ToolMenuClicked = OnToolMenuClicked,
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
        _view = new ImageViewerView(
            session,
            toolMenu,
            settingControls,
            initialWindowMode,
            viewEvents);
        _interaction =
            ImageViewerWindowInteractionComposition.Create(
            this,
            _view,
            _session,
            information,
            OnOpenWithApplicationClicked,
            OnChooseApplicationClicked,
            Close,
            logger,
            actions,
            openWith,
            imagePresentation,
            presentationReadiness,
            _animationFrameScheduler,
            settings,
            windowPlacementProvider);
        _view.ContextOpenWithButton.IsVisible = openWith.IsSupported;
        _view.SelectionOpenWithButton.IsVisible = openWith.IsSupported;
        _view.ApplyImageFiltering(settings.IsFilteringEnabled);

        ConfigureWindow();
        _interaction.Presentation.ApplyInformation();
        AttachEvents();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        UpdateAnimationFramePresentation();
        ReadyForLoading?.Invoke(this, EventArgs.Empty);
        _windowMode.ApplyInitialMode();
        _view.FadeOverlay.Opacity = _view.HiddenControlsOpacity;
        _cursorController.Start();
    }

    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        await _interaction.Close.HandleClosingAsync(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewport.StopScaleAnimation();
        _viewport.StopPanMotion();
        _animationFrameScheduler.CancelPendingFrames();
        _interaction.Dispose();
        _animationFrameScheduler.AnimationFrameRequested -=
            OnAnimationFrameRequested;
        _view.Image.Source = null;
        _view.Dispose();
        _logoBitmap.Dispose();
        base.OnClosed(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if ((change.Property == IsVisibleProperty)
            || (change.Property == WindowStateProperty))
        {
            UpdateAnimationFramePresentation();
        }

        if (change.Property == WindowStateProperty)
        {
            _windowMode.HandleWindowStateChanged();
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

    private void UpdateAnimationFramePresentation()
    {
        ViewerAnimationFrameScheduler? animationFrameScheduler =
            _animationFrameScheduler;
        animationFrameScheduler?.SetPresentation(
            IsVisible && (WindowState != WindowState.Minimized));
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
            Source = _logoBitmap,
            Stretch = Stretch.Uniform
        };
        MinWidth = MinimumWindowWidth;
        RightWindowTitleBarControls = _view.TitleBarSettingsControls;
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
        Content = _view;
        _windowMode.ApplyConfiguredGeometry();
    }

    private void AttachEvents()
    {
        _view.ViewerArea.PointerPressed +=
            _pointerInput.OnPointerPressed;
        _view.ViewerArea.PointerMoved +=
            _pointerInput.OnPointerMoved;
        _view.ViewerArea.PointerReleased +=
            _pointerInput.OnPointerReleased;
        _view.ViewerArea.PointerWheelChanged +=
            _pointerInput.OnPointerWheelChanged;
        _view.ViewerArea.SizeChanged += OnViewerAreaSizeChanged;
        _animationFrameScheduler.AnimationFrameRequested +=
            OnAnimationFrameRequested;
        AddHandler(
            KeyDownEvent,
            _keyboardInput.OnPreviewKeyDown,
            RoutingStrategies.Tunnel);
        KeyDown += _keyboardInput.OnKeyDown;
        KeyUp += _keyboardInput.OnKeyUp;
        PositionChanged += OnWindowPositionChanged;
        Resized += OnWindowResized;
        _view.LeftNavigationArea.PointerPressed += OnLeftNavigationPressed;
        _view.RightNavigationArea.PointerPressed += OnRightNavigationPressed;
        _view.ViewerContextMenu.PointerPressed += OnFloatingMenuPointerPressed;
        _view.ContextOpenWithButton.PointerEntered +=
            _floatingMenus.OnContextOpenWithAnchorPointerEntered;
        _view.ContextOpenWithButton.PointerExited +=
            _floatingMenus.OnSubmenuAnchorPointerExited;
        _view.SelectionOpenWithButton.PointerExited +=
            _floatingMenus.OnSubmenuAnchorPointerExited;
        _view.OpenWithMenu.PointerEntered +=
            _floatingMenus.OnSubmenuPointerEntered;
        _view.OpenWithMenu.PointerExited +=
            _floatingMenus.OnSubmenuPointerExited;
        _view.OpenWithMenu.PointerPressed += OnFloatingMenuPointerPressed;
        _view.ToolMenu.PointerPressed += OnFloatingMenuPointerPressed;
        _view.ModeMenuButton.PointerEntered +=
            _floatingMenus.OnModeMenuAnchorPointerEntered;
        _view.ModeMenuButton.PointerExited +=
            _floatingMenus.OnSubmenuAnchorPointerExited;
        _view.ModeMenu.PointerEntered +=
            _floatingMenus.OnSubmenuPointerEntered;
        _view.ModeMenu.PointerExited +=
            _floatingMenus.OnSubmenuPointerExited;
        _view.ModeMenu.PointerPressed += OnFloatingMenuPointerPressed;
        _view.SelectionToolbar.PointerPressed += OnFloatingMenuPointerPressed;
        _view.SettingsPanel.PointerPressed += OnFloatingMenuPointerPressed;
        _view.Root.PointerExited +=
            _pointerInput.OnRootPointerExited;
    }

    private void OnAnimationFrameRequested(
        object? sender,
        ViewerAnimationFrameRequestedEventArgs e)
    {
        _ = sender;
        RequestAnimationFrame(e.FrameAction);
    }
}
