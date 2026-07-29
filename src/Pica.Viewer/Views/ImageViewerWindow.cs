using Microsoft.Extensions.Logging;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using SukiUI.Controls;

using Pica.Protocol;
using Pica.Viewer.Controls;
using Pica.Viewer.Resources;
using Pica.Viewer.Services;

namespace Pica.Viewer.Views;

public sealed partial class ImageViewerWindow : SukiWindow
{
    private const string AppIconAssetUri = "avares://Pica.Viewer/Assets/AppIcon.ico";
    private const double EdgeRevealRatio = 0.04d;
    private const double BottomRevealSize = 128d;
    private const double ContextMenuGap = 8d;
    private const double ContextMenuFallbackWidth = 172d;
    private const double ContextMenuFallbackHeight = 260d;
    private const double OpenWithMenuFallbackWidth = 220d;
    private const double OpenWithMenuFallbackHeight = 52d;
    private const double ToolMenuFallbackWidth = 160d;
    private const double ToolMenuFallbackHeight = 86d;
    private const double ModeMenuFallbackWidth = 160d;
    private const double ModeMenuFallbackHeight = 86d;
    private const double SelectionToolbarGap = 10d;
    private const double SelectionHandleSize = 8d;
    private const double MinimumSelectionSize = 12d;
    private const double DefaultZoomButtonFactor = 1.2d;
    private const double WheelZoomBase = 1.0015d;
    private const double SettingsPanelAnimationDurationSeconds = 0.16d;
    private const double ScaleAnimationDurationSeconds = 0.14d;
    private const double CopyFeedbackOpacity = 0.44d;
    private const double CopyFeedbackFadeInDurationSeconds = 0.1d;
    private const double CopyFeedbackFadeOutDurationSeconds = 0.08d;
    private const double MinimumScale = 0.05d;
    private const double MaximumScale = 32d;
    private const int CursorHideDelayMilliseconds = 1000;
    private const int SubmenuHideDelayMilliseconds = 120;
    private const int WindowModeLayoutSettleDelayMilliseconds = 100;
    private const double DefaultWindowWidth = 1280d;
    private const double DefaultWindowHeight = 800d;
    private const double WindowedTitleBarHeight = 36d;
    private const double MinimumWindowWidth = 300d;
    private const double TitleLogoSize = 28d;

    private static readonly TimeSpan ScaleAnimationDuration = TimeSpan.FromSeconds(ScaleAnimationDurationSeconds);
    private static readonly TimeSpan CopyFeedbackFadeInDuration =
        TimeSpan.FromSeconds(CopyFeedbackFadeInDurationSeconds);
    private static readonly TimeSpan CopyFeedbackFadeOutDuration =
        TimeSpan.FromSeconds(CopyFeedbackFadeOutDurationSeconds);
    private static readonly TimeSpan SettingsPanelAnimationDuration =
        TimeSpan.FromSeconds(SettingsPanelAnimationDurationSeconds);
    private static readonly double ZoomButtonStepBase =
        Math.Pow(DefaultZoomButtonFactor, 1d / ViewerSettingsDefaults.ZoomSpeed);
    private readonly PicaViewerRequest _request;
    private readonly IImageViewerStateService _imageViewerStateService;
    private readonly ImagePreviewLoader _imagePreviewLoader;
    private readonly FullResolutionImageLoader _fullResolutionImageLoader;
    private readonly ImageChannelBitmapLoader _imageChannelBitmapLoader;
    private readonly ClipboardImagePreparer _clipboardImagePreparer;
    private readonly TemporaryImageFileStore _temporaryImageFileStore;
    private readonly IPlatformFileActions _platformFileActions;
    private readonly ILogger<ImageViewerWindow> _logger;
    private readonly ViewerImageOperations _imageOperations;
    private readonly ImageDoubleClickTracker _imageDoubleClickTracker;
    private readonly ImagePanMotion _panMotion;
    private readonly ViewerAnimationFrameScheduler _animationFrameScheduler;
    private readonly Bitmap _logoBitmap;
    private readonly ImageViewerView _view;
    private readonly DispatcherTimer _cursorTimer;
    private readonly DispatcherTimer _windowModeLayoutTimer;
    private readonly DispatcherTimer _submenuHideTimer;
    private readonly ImagePreviewCache _previewCache;
    private readonly ImageChannelSelection _channelSelection;
    private readonly ImageViewerState _settings;
    private Bitmap? _bitmap;
    private Bitmap? _sourceBitmap;
    private Bitmap? _channelBitmap;
    private PicaImageItem? _currentItem;
    private PixelSize _sourcePixelSize;
    private int _selectedIndex;
    private int _preferredNavigationDirection = 1;
    private double _scale = 1d;
    private double _offsetX;
    private double _offsetY;
    private bool _isPointerPressed;
    private bool _isPanning;
    private bool _isSelecting;
    private bool _isSelectionActive;
    private bool _isSelectionMoving;
    private bool _isSelectionArmed;
    private bool _isControlModifierActive;
    private bool _isFullResolutionImageReady;
    private bool _isImageOperationRunning;
    private KeyModifiers _activeKeyModifiers;
    private SelectionResizeMode _selectionResizeMode;
    private Point _pointerPressPosition;
    private Point _lastPointerPosition;
    private Point _lastPointerHoverPosition;
    private PixelPoint? _lastPointerScreenPosition;
    private Point _selectionStartPosition;
    private Rect _selectionRect;
    private Rect _selectionStartRect;
    private PixelRect _selectionPixelRect;
    private PixelRect _selectionStartPixelRect;
    private long _scaleAnimationId;
    private long _copyFeedbackAnimationId;
    private long _selectionOverlayAnimationId;
    private long _settingsPanelAnimationId;
    private long _imageLoadId;
    private long _channelLoadId;
    private bool _isChangingWindowMode;
    private bool _isWindowedMode;
    private bool _isApplyingWindowGeometry;
    private bool _isImageClickCandidate;
    private bool _isCursorHidden;
    private bool _isSavingStateBeforeClose;
    private bool _isClosingAfterStateSave;
    private bool _isWindowResizeLayoutPending;
    private bool _isWindowModeLayoutSettling;
    private bool _isPanAnimationFramePending;
    private DateTimeOffset _lastPanAnimationFrameTimestamp;
    private PixelPoint? _windowedPosition;
    private Size? _windowedClientSize;
    private double _windowedPreferredExtent;
    private IWindowResizeSession? _windowResizeSession;
    private CancellationTokenSource? _imageLoadCancellation;
    private CancellationTokenSource? _channelLoadCancellation;
    private CancellationTokenSource? _selectionPreparationCancellation;
    private Task? _activeImageLoadTask;
    private Task? _activeChannelLoadTask;
    private Task? _previewCachePrimingTask;
    private Task<PreparedClipboardImage?>? _selectionPreparationTask;
    private PixelRect _selectionPreparationRect;
    private OpenWithTarget _openWithTarget;
    private Control? _submenuAnchor;
    private Border? _activeSubmenu;
    private ImageChannel? _displayedChannel;

    internal ImageViewerWindow(
        PicaViewerRequest request,
        IViewerClipboardWriter clipboardImageWriter,
        IImageFormatRegistry formatRegistry,
        IImageViewerStateService imageViewerStateService,
        ImagePreviewLoader imagePreviewLoader,
        FullResolutionImageLoader fullResolutionImageLoader,
        ImageChannelBitmapLoader imageChannelBitmapLoader,
        PngImageEncoder pngImageEncoder,
        ClipboardImagePreparer clipboardImagePreparer,
        TemporaryImageFileStore temporaryImageFileStore,
        IPlatformFileActions platformFileActions,
        IViewerActionDispatcher actionDispatcher,
        ILogger<ImageViewerWindow> logger,
        ImageViewerState initialState)
    {
        _request = request ?? throw new ArgumentNullException(nameof(request));
        _imageViewerStateService = imageViewerStateService
            ?? throw new ArgumentNullException(nameof(imageViewerStateService));
        _imagePreviewLoader = imagePreviewLoader
            ?? throw new ArgumentNullException(nameof(imagePreviewLoader));
        _fullResolutionImageLoader = fullResolutionImageLoader
            ?? throw new ArgumentNullException(nameof(fullResolutionImageLoader));
        _imageChannelBitmapLoader = imageChannelBitmapLoader
            ?? throw new ArgumentNullException(nameof(imageChannelBitmapLoader));
        _clipboardImagePreparer = clipboardImagePreparer
            ?? throw new ArgumentNullException(nameof(clipboardImagePreparer));
        _temporaryImageFileStore = temporaryImageFileStore
            ?? throw new ArgumentNullException(nameof(temporaryImageFileStore));
        _platformFileActions = platformFileActions
            ?? throw new ArgumentNullException(nameof(platformFileActions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _imageOperations = new ViewerImageOperations(
            clipboardImageWriter,
            formatRegistry,
            pngImageEncoder,
            actionDispatcher);
        ArgumentNullException.ThrowIfNull(initialState);
        _settings = initialState.CreateCopy();
        _selectedIndex = GetItemIndexOrDefault(request.Items, request.SelectedItemId);
        _isWindowedMode = _settings.RememberWindowPlacement && (_settings.IsWindowed == true);
        _windowedPosition = _settings.RememberWindowPlacement
            ? CreateWindowedPosition(_settings)
            : null;
        _windowedClientSize = _settings.RememberWindowPlacement
            ? CreateWindowedClientSize(_settings)
            : null;
        Size initialWindowedClientSize = _windowedClientSize
            ?? new Size(DefaultWindowWidth, DefaultWindowHeight);
        _windowedPreferredExtent = Math.Max(
            initialWindowedClientSize.Width,
            Math.Max(1d, initialWindowedClientSize.Height - WindowedTitleBarHeight));
        _imageDoubleClickTracker = new ImageDoubleClickTracker();
        _panMotion = new ImagePanMotion();
        _animationFrameScheduler = new ViewerAnimationFrameScheduler(
            RequestAnimationFrame);
        _previewCache = new ImagePreviewCache();
        _channelSelection = new ImageChannelSelection();
        _logoBitmap = LoadBitmap(AppIconAssetUri);
        ImageViewerViewEvents viewEvents = new()
        {
            ZoomOutClicked = OnZoomOutClicked,
            ResetClicked = OnResetClicked,
            ZoomInClicked = OnZoomInClicked,
            ToolMenuClicked = OnToolMenuClicked,
            FilteringMenuClicked = OnFilteringMenuClicked,
            ModeMenuClicked = OnModeMenuClicked,
            MainModeMenuClicked = OnMainModeMenuClicked,
            ChannelModeMenuClicked = OnChannelModeMenuClicked,
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
        IReadOnlyList<ViewerSettingControl> settingControls = CreateSettingControls();
        _view = new ImageViewerView(
            _settings.IsFilteringEnabled,
            settingControls,
            request.Actions,
            GetViewerWindowMode(),
            viewEvents);
        _view.ContextOpenWithButton.IsVisible = _platformFileActions.SupportsOpenWith;
        _view.SelectionOpenWithButton.IsVisible = _platformFileActions.SupportsOpenWith;
        _view.ApplyImageFiltering(_settings.IsFilteringEnabled);
        _cursorTimer = CreateCursorTimer();
        _windowModeLayoutTimer = CreateWindowModeLayoutTimer();
        _submenuHideTimer = CreateSubmenuHideTimer();

        ConfigureWindow();
        UpdateSelectedImageInformation();
        AttachEvents();
    }

    internal async Task PersistStateAsync(CancellationToken ct)
    {
        CaptureWindowedPlacement();
        await _imageViewerStateService.SaveAsync(CreateState(), ct);
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        UpdateAnimationFramePresentation();
        _logger.LogInformation(
            "Pica viewer opened with {ItemCount} images in {WindowMode} mode",
            _request.Items.Count,
            GetViewerWindowMode());

        if (Clipboard is { } clipboard)
        {
            _imageOperations.AttachClipboard(clipboard);
        }

        LoadSelectedImage();
        ApplyInitialWindowMode();
        _view.FadeOverlay.Opacity = ImageViewerVisualMetrics.HiddenControlsOpacity;
        _cursorTimer.Start();
    }

    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        if (_isClosingAfterStateSave)
        {
            base.OnClosing(e);
            return;
        }

        base.OnClosing(e);

        if (e.Cancel)
        {
            return;
        }

        e.Cancel = true;

        if (_isSavingStateBeforeClose)
        {
            return;
        }

        _isSavingStateBeforeClose = true;

        try
        {
            await PersistStateAsync(CancellationToken.None);
            _logger.LogDebug("Persisted Pica viewer state before closing");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save the Pica window position before closing.");
        }

        _isClosingAfterStateSave = true;
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _logger.LogInformation("Pica viewer closed");
        _ = _imageOperations.FlushClipboardAsync(CancellationToken.None);
        base.OnClosed(e);

        _cursorTimer.Stop();
        _windowModeLayoutTimer.Stop();
        _submenuHideTimer.Stop();
        StopScaleAnimation();
        StopPanMotion();
        CancelPendingImageLoad();
        _animationFrameScheduler.CancelPendingFrames();
        CancelSelectionClipboardPreparation();
        _view.Dispose();
        _temporaryImageFileStore.Dispose();
        _previewCache.Dispose();
        ReleaseDisplayedBitmap();
        _logoBitmap.Dispose();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if ((change.Property == IsVisibleProperty)
            || (change.Property == WindowStateProperty))
        {
            UpdateAnimationFramePresentation();
        }

        if ((change.Property != WindowStateProperty) || _isChangingWindowMode)
        {
            return;
        }

        if ((WindowState == WindowState.FullScreen) || (WindowState == WindowState.Maximized))
        {
            EnterFullScreenMode();
        }
        else if ((WindowState == WindowState.Normal) && !_isWindowedMode)
        {
            EnterWindowedMode();
        }
    }

    private void UpdateAnimationFramePresentation()
    {
        ViewerAnimationFrameScheduler? animationFrameScheduler =
            _animationFrameScheduler;
        animationFrameScheduler?.SetPresentation(
            IsVisible && (WindowState != WindowState.Minimized));
    }

    private void RequestViewerAnimationFrame(Action<TimeSpan> frameAction)
    {
        _animationFrameScheduler.RequestAnimationFrame(frameAction);
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
        IsTitleBarVisible = _isWindowedMode;
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
        ShowTitlebarBackground = _isWindowedMode;
        Title = PicaProtocolConstants.ApplicationName;
        TitleFontWeight = FontWeight.Normal;
        TitleBarAnimationEnabled = false;
        TitleBarVisibilityOnFullScreen = TitleBarVisibilityMode.Hidden;
        WindowState = _isWindowedMode
            ? WindowState.Normal
            : WindowState.FullScreen;
        Content = _view.Root;

        if (_isWindowedMode && (_windowedClientSize is { } windowedClientSize))
        {
            ApplyWindowedClientSize(windowedClientSize);
            ApplyWindowedPosition(windowedClientSize);
        }
    }

    private static Bitmap LoadBitmap(string assetUri)
    {
        using Stream stream = AssetLoader.Open(new Uri(assetUri));

        return new Bitmap(stream);
    }

    private static WindowIcon LoadWindowIcon(string assetUri)
    {
        using Stream stream = AssetLoader.Open(new Uri(assetUri));

        return new WindowIcon(stream);
    }

    private DispatcherTimer CreateCursorTimer()
    {
        DispatcherTimer timer = new()
        {
            Interval = TimeSpan.FromMilliseconds(CursorHideDelayMilliseconds)
        };
        timer.Tick += OnCursorTimerTick;

        return timer;
    }

    private DispatcherTimer CreateWindowModeLayoutTimer()
    {
        DispatcherTimer timer = new()
        {
            Interval = TimeSpan.FromMilliseconds(WindowModeLayoutSettleDelayMilliseconds)
        };
        timer.Tick += OnWindowModeLayoutTimerTick;

        return timer;
    }

    private DispatcherTimer CreateSubmenuHideTimer()
    {
        DispatcherTimer timer = new()
        {
            Interval = TimeSpan.FromMilliseconds(SubmenuHideDelayMilliseconds)
        };
        timer.Tick += OnSubmenuHideTimerTick;

        return timer;
    }

    private void AttachEvents()
    {
        _view.ViewerArea.PointerPressed += OnPointerPressed;
        _view.ViewerArea.PointerMoved += OnPointerMoved;
        _view.ViewerArea.PointerReleased += OnPointerReleased;
        _view.ViewerArea.PointerWheelChanged += OnPointerWheelChanged;
        _view.ViewerArea.SizeChanged += OnViewerAreaSizeChanged;
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        KeyDown += OnKeyDown;
        KeyUp += OnKeyUp;
        PositionChanged += OnWindowPositionChanged;
        Resized += OnWindowResized;
        _view.LeftNavigationArea.PointerPressed += OnLeftNavigationPressed;
        _view.RightNavigationArea.PointerPressed += OnRightNavigationPressed;
        _view.ContextMenu.PointerPressed += OnFloatingMenuPointerPressed;
        _view.ContextOpenWithButton.PointerEntered += OnContextOpenWithAnchorPointerEntered;
        _view.ContextOpenWithButton.PointerExited += OnSubmenuAnchorPointerExited;
        _view.SelectionOpenWithButton.PointerExited += OnSubmenuAnchorPointerExited;
        _view.OpenWithMenu.PointerEntered += OnSubmenuPointerEntered;
        _view.OpenWithMenu.PointerExited += OnSubmenuPointerExited;
        _view.OpenWithMenu.PointerPressed += OnFloatingMenuPointerPressed;
        _view.ToolMenu.PointerPressed += OnFloatingMenuPointerPressed;
        _view.ModeMenuButton.PointerEntered += OnModeMenuAnchorPointerEntered;
        _view.ModeMenuButton.PointerExited += OnSubmenuAnchorPointerExited;
        _view.ModeMenu.PointerEntered += OnSubmenuPointerEntered;
        _view.ModeMenu.PointerExited += OnSubmenuPointerExited;
        _view.ModeMenu.PointerPressed += OnFloatingMenuPointerPressed;
        _view.SelectionToolbar.PointerPressed += OnFloatingMenuPointerPressed;
        _view.SettingsPanel.PointerPressed += OnFloatingMenuPointerPressed;
        _view.Root.PointerExited += OnRootPointerExited;
    }

    [Flags]
    private enum SelectionResizeMode
    {
        None = 0,
        Left = 1,
        Right = 2,
        Top = 4,
        Bottom = 8,
        TopLeft = Top | Left,
        TopRight = Top | Right,
        BottomRight = Bottom | Right,
        BottomLeft = Bottom | Left
    }
}
