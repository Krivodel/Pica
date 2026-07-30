using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;

using Pica.Protocol;
using Pica.Viewer.Controls;
using Pica.Viewer.Resources;
using Pica.Viewer.Services;
using Pica.Viewer.ViewModels;

using ShapePath = Avalonia.Controls.Shapes.Path;

namespace Pica.Viewer.Views;

internal sealed partial class ImageViewerView : UserControl, IDisposable
{
    internal Grid Root { get; }
    internal Grid ViewerArea { get; }
    internal Grid WindowResizeOverlay { get; }
    internal ViewerSettingsPanel SettingsPanel { get; }
    internal Border FadeOverlay { get; }
    internal Canvas ImageCanvas { get; }
    internal Border CheckerboardBackground { get; }
    internal Border CheckerboardPattern { get; }
    internal Image Image { get; }
    internal Border LeftNavigationArea { get; }
    internal Border RightNavigationArea { get; }
    internal Grid BottomControls { get; }
    internal Button ToolMenuButton { get; }
    internal Canvas ToolMenuLayer { get; }
    internal Border ToolMenu { get; }
    internal Button ModeMenuButton { get; }
    internal Border ModeMenu { get; }
    internal Border ImageInformationPanel { get; }
    internal TextBlock ImageInformationText { get; }
    internal Button FullscreenSettingsButton { get; }
    internal Button WindowModeButton { get; }
    internal Button CloseButton { get; }
    internal Canvas ContextMenuLayer { get; }
    internal Border ViewerContextMenu { get; }
    internal Button ContextOpenWithButton { get; }
    internal Canvas OpenWithMenuLayer { get; }
    internal Border OpenWithMenu { get; }
    internal StackPanel OpenWithMenuItems { get; }
    internal Canvas SelectionOverlay { get; }
    internal ShapePath SelectionShade { get; }
    internal ShapePath SelectionFrame { get; }
    internal StackPanel SelectionToolbar { get; }
    internal Button SelectionOpenWithButton { get; }
    internal Avalonia.Controls.Controls TitleBarSettingsControls { get; }
    internal double HiddenControlsOpacity { get; }
    internal double InformationPanelMargin => ImageInformationPanel.Margin.Left;
    internal double NavigationAreaMinimumWidth { get; }
    internal double VisibleControlsOpacity { get; }
    internal double WindowButtonSize { get; }
    internal double WindowControlsWidth => WindowButtonSize * 3d;

    private const string CopyIconGeometry = "M8,7 L17,7 L17,19 L8,19 Z M6,5 L15,5 L15,3 L4,3 L4,15 L6,15 Z";
    private const string SaveIconGeometry = "M5,3 L16,3 L21,8 L21,19 L19,21 L5,21 L3,19 L3,5 Z M7,6 L7,10 L11,10 L11,8 L13,8 L13,10 L16,10 L16,6 Z M7,19 L17,19 L17,14 L7,14 Z";
    private const string FolderIconGeometry = "M3,6 L10,6 L12,8 L21,8 L21,19 L3,19 Z";
    private const string OpenWithIconGeometry = "M13,3 L20,3 L20,10 L18,10 L18,6.4 L9.4,15 L8,13.6 L16.6,5 L13,5 Z M4,5 L10,5 L10,7 L6,7 L6,17 L16,17 L16,13 L18,13 L18,19 L4,19 Z";
    private const string MenuForegroundBrushResourceKey = "ViewerMenuForegroundBrush";
    private const string DestructiveIconBrushResourceKey = "ViewerDestructiveIconBrush";
    private const string FloatingControlShadowEffectResourceKey =
        "ViewerFloatingControlShadowEffect";
    private const string HiddenControlsOpacityResourceKey =
        "ViewerHiddenControlsOpacity";
    private const string NavigationAreaMinimumWidthResourceKey =
        "ViewerNavigationAreaMinimumWidth";
    private const string SettingsIconClassName = "settings-icon";
    private const string VisibleControlsOpacityResourceKey =
        "ViewerVisibleControlsOpacity";
    private const string WindowButtonSizeResourceKey =
        "ViewerWindowButtonSize";
    private const string WindowIconHostSizeResourceKey =
        "ViewerWindowIconHostSize";
    private const double SettingsPanelTopGap = 8d;
    private const double SettingsPanelRightMargin = 12d;
    private const double SelectionButtonSize = 42d;
    private const double SelectionButtonSpacing = 6d;
    private const double SelectionToolbarPadding = 8d;
    private readonly List<Bitmap> _openWithIcons = [];
    private readonly ImageViewerToolMenuControl _toolMenuControl;
    private readonly Grid _viewerDynamicLayer;
    private readonly double _checkerboardTileSize;
    private readonly TranslateTransform _checkerboardPatternTransform;

    internal ImageViewerView(
        ImageViewerSessionViewModel session,
        ImageViewerToolMenuViewModel toolMenu,
        IReadOnlyList<ViewerSettingControl> settingControls,
        ViewerWindowMode windowMode,
        ImageViewerViewEvents events)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(toolMenu);
        ArgumentNullException.ThrowIfNull(settingControls);
        ArgumentNullException.ThrowIfNull(events);

        InitializeComponent();
        HiddenControlsOpacity =
            GetRequiredDouble(HiddenControlsOpacityResourceKey);
        NavigationAreaMinimumWidth =
            GetRequiredDouble(NavigationAreaMinimumWidthResourceKey);
        VisibleControlsOpacity =
            GetRequiredDouble(VisibleControlsOpacityResourceKey);
        WindowButtonSize =
            GetRequiredDouble(WindowButtonSizeResourceKey);
        Root = this.FindControl<Grid>("RootControl")
            ?? throw new InvalidOperationException(
                "The image viewer is missing its root grid.");
        ViewerArea = this.FindControl<Grid>("ViewerAreaControl")
            ?? throw new InvalidOperationException(
                "The image viewer is missing its viewer area.");
        ImageCanvas = this.FindControl<Canvas>("ImageCanvasControl")
            ?? throw new InvalidOperationException(
                "The image viewer is missing its image canvas.");
        CheckerboardBackground =
            this.FindControl<Border>("CheckerboardBackgroundControl")
            ?? throw new InvalidOperationException(
                "The image viewer is missing its checkerboard background.");
        CheckerboardPattern =
            this.FindControl<Border>("CheckerboardPatternControl")
            ?? throw new InvalidOperationException(
                "The image viewer is missing its checkerboard pattern.");
        VisualBrush checkerboardBrush =
            CheckerboardPattern.Background as VisualBrush
            ?? throw new InvalidOperationException(
                "The image viewer is missing its checkerboard brush.");
        _checkerboardTileSize =
            checkerboardBrush.DestinationRect.Rect.Width;

        if (_checkerboardTileSize <= 0d)
        {
            throw new InvalidOperationException(
                "The image viewer checkerboard tile must have a positive size.");
        }

        _checkerboardPatternTransform = new TranslateTransform();
        CheckerboardPattern.Margin = new Thickness(-_checkerboardTileSize);
        CheckerboardPattern.RenderTransformOrigin = RelativePoint.TopLeft;
        CheckerboardPattern.RenderTransform = _checkerboardPatternTransform;
        Image = this.FindControl<Image>("ImageControl")
            ?? throw new InvalidOperationException(
                "The image viewer is missing its image control.");
        _viewerDynamicLayer = this.FindControl<Grid>("ViewerDynamicLayerControl")
            ?? throw new InvalidOperationException(
                "The image viewer is missing its dynamic layer.");
        FadeOverlay = this.FindControl<Border>("FadeOverlayControl")
            ?? throw new InvalidOperationException(
                "The image viewer is missing its fade overlay.");
        WindowResizeOverlay = this.FindControl<Grid>("WindowResizeOverlayControl")
            ?? throw new InvalidOperationException(
                "The image viewer is missing its window resize overlay.");
        WindowResizeOverlay.IsVisible = windowMode == ViewerWindowMode.Windowed;
        ConfigureWindowResizeBorders(WindowResizeOverlay, events);
        SettingsPanel = CreateSettingsPanel(
            settingControls,
            windowMode,
            HiddenControlsOpacity,
            WindowButtonSize);
        LeftNavigationArea = this.FindControl<Border>("LeftNavigationAreaControl")
            ?? throw new InvalidOperationException(
                "The image viewer is missing its left navigation area.");
        RightNavigationArea = this.FindControl<Border>("RightNavigationAreaControl")
            ?? throw new InvalidOperationException(
                "The image viewer is missing its right navigation area.");
        BottomControls = this.FindControl<Grid>("BottomControlsControl")
            ?? throw new InvalidOperationException(
                "The image viewer is missing its bottom controls.");
        Button zoomOutButton = GetRequiredButton("ZoomOutButton");
        Button resetButton = GetRequiredButton("ResetButton");
        Button zoomInButton = GetRequiredButton("ZoomInButton");
        ToolMenuButton = GetRequiredButton("ToolMenuButtonControl");
        zoomOutButton.Click += events.ZoomOutClicked;
        resetButton.Click += events.ResetClicked;
        zoomInButton.Click += events.ZoomInClicked;
        ToolMenuButton.Click += events.ToolMenuClicked;
        ImageViewerToolMenuControl toolMenuControl = new(toolMenu);
        toolMenuControl.CheckerboardBackgroundMenuItem.Click +=
            events.CheckerboardBackgroundMenuClicked;
        toolMenuControl.FilteringMenuItem.Click += events.FilteringMenuClicked;
        toolMenuControl.ModeMenuButton.Click += events.ModeMenuClicked;
        toolMenuControl.MainModeMenuItem.Click += events.MainModeMenuClicked;
        toolMenuControl.ChannelModeMenuItem.Click += events.ChannelModeMenuClicked;
        _toolMenuControl = toolMenuControl;
        ToolMenuLayer = toolMenuControl.MenuLayer;
        ToolMenu = toolMenuControl.ToolMenu;
        ModeMenuButton = toolMenuControl.ModeMenuButton;
        ModeMenu = toolMenuControl.ModeMenu;
        ImageInformationPanel = this.FindControl<Border>("ImageInformationPanelControl")
            ?? throw new InvalidOperationException(
                "The image viewer is missing its information panel.");
        ImageInformationPanel.IsVisible =
            windowMode == ViewerWindowMode.FullScreen;
        ImageInformationText = this.FindControl<TextBlock>("ImageInformationTextControl")
            ?? throw new InvalidOperationException(
                "The image viewer is missing its information text.");
        FullscreenSettingsButton = GetRequiredButton(
            "FullscreenSettingsButtonControl");
        WindowModeButton = GetRequiredButton("WindowModeButtonControl");
        CloseButton = GetRequiredButton("CloseButtonControl");
        PathIcon closeIcon = GetRequiredPathIcon("CloseIconControl");
        closeIcon.Data = ViewerIconGeometries.CloseOrCancel;
        FullscreenSettingsButton.Click += events.SettingsClicked;
        WindowModeButton.Click += events.WindowModeClicked;
        CloseButton.Click += events.CloseClicked;
        ContextMenuLayer = CreateClippedMenuLayer();
        ViewerContextMenu = CreateContextMenu(
            session.Actions,
            events,
            out Button contextOpenWithButton);
        ContextOpenWithButton = contextOpenWithButton;
        OpenWithMenuLayer = CreateClippedMenuLayer();
        OpenWithMenu = CreateOpenWithMenu(out StackPanel openWithMenuItems);
        OpenWithMenuItems = openWithMenuItems;
        IBrush menuForegroundBrush =
            GetRequiredBrush(MenuForegroundBrushResourceKey);
        IBrush destructiveIconBrush =
            GetRequiredBrush(DestructiveIconBrushResourceKey);
        SelectionOverlay = CreateSelectionOverlay(
            session.Actions,
            events,
            menuForegroundBrush,
            destructiveIconBrush,
            HiddenControlsOpacity,
            out ShapePath selectionShade,
            out ShapePath selectionFrame,
            out StackPanel selectionToolbar,
            out Button selectionOpenWithButton);
        SelectionShade = selectionShade;
        SelectionFrame = selectionFrame;
        SelectionToolbar = selectionToolbar;
        SelectionOpenWithButton = selectionOpenWithButton;
        IEffect floatingControlShadow =
            GetRequiredEffect(FloatingControlShadowEffectResourceKey);
        double windowIconHostSize =
            GetRequiredDouble(WindowIconHostSizeResourceKey);
        TitleBarSettingsControls = CreateTitleBarSettingsButton(
            events.SettingsClicked,
            floatingControlShadow,
            windowIconHostSize);
        Compose();
    }

    public void Dispose()
    {
        DisposeOpenWithIcons();
    }

    internal void UpdateSettingsPanelPlacement(ViewerWindowMode windowMode)
    {
        SettingsPanel.Margin = CreateSettingsPanelMargin(
            windowMode,
            WindowButtonSize);
    }

    internal void ApplyImageFiltering(bool isFilteringEnabled)
    {
        RenderOptions.SetBitmapInterpolationMode(
            Image,
            isFilteringEnabled
                ? BitmapInterpolationMode.HighQuality
                : BitmapInterpolationMode.None);
    }

    internal void ApplyCheckerboardBackground(bool isEnabled)
    {
        CheckerboardBackground.IsVisible = isEnabled;
    }

    internal void UpdateCheckerboardPatternOffset(
        double offsetX,
        double offsetY)
    {
        _checkerboardPatternTransform.X =
            NormalizeCheckerboardPatternOffset(offsetX);
        _checkerboardPatternTransform.Y =
            NormalizeCheckerboardPatternOffset(offsetY);
    }

    internal void UpdateImageInformation(string information)
    {
        ArgumentNullException.ThrowIfNull(information);
        ImageInformationText.Text = information;
    }

    internal void UpdateOpenWithApplications(
        IReadOnlyList<OpenWithApplication> applications,
        EventHandler<RoutedEventArgs> applicationClickHandler,
        EventHandler<RoutedEventArgs> chooseApplicationClickHandler)
    {
        ArgumentNullException.ThrowIfNull(applications);
        ArgumentNullException.ThrowIfNull(applicationClickHandler);
        ArgumentNullException.ThrowIfNull(chooseApplicationClickHandler);
        DisposeOpenWithIcons();
        OpenWithMenuItems.Children.Clear();

        foreach (OpenWithApplication application in applications)
        {
            Bitmap? icon = CreateOpenWithApplicationIcon(application.IconPngContent);

            if (icon is not null)
            {
                _openWithIcons.Add(icon);
            }

            Button button = CreateOpenWithApplicationMenuButton(
                application.DisplayName,
                icon,
                applicationClickHandler);
            button.Tag = application;
            OpenWithMenuItems.Children.Add(button);
        }

        OpenWithMenuItems.Children.Add(CreateTextMenuButton(
            "Выбрать другое приложение…",
            chooseApplicationClickHandler));
    }

    private static ViewerSettingsPanel CreateSettingsPanel(
        IReadOnlyList<ViewerSettingControl> settingControls,
        ViewerWindowMode windowMode,
        double hiddenControlsOpacity,
        double windowButtonSize)
    {
        return new ViewerSettingsPanel(settingControls)
        {
            Margin = CreateSettingsPanelMargin(
                windowMode,
                windowButtonSize),
            HorizontalAlignment = HorizontalAlignment.Right,
            IsHitTestVisible = false,
            IsVisible = false,
            Opacity = hiddenControlsOpacity,
            RenderTransform = new TranslateTransform(
                0d,
                ImageViewerVisualMetrics.SettingsPanelHiddenOffset),
            VerticalAlignment = VerticalAlignment.Top
        };
    }

    private static Thickness CreateSettingsPanelMargin(
        ViewerWindowMode windowMode,
        double windowButtonSize)
    {
        double topMargin = windowMode == ViewerWindowMode.Windowed
            ? SettingsPanelTopGap
            : windowButtonSize + SettingsPanelTopGap;

        return new Thickness(0d, topMargin, SettingsPanelRightMargin, 0d);
    }

    private static Avalonia.Controls.Controls CreateTitleBarSettingsButton(
        EventHandler<RoutedEventArgs> clickHandler,
        IEffect floatingControlShadow,
        double windowIconHostSize)
    {
        PathIcon icon = new();
        icon.Classes.Add(SettingsIconClassName);
        Button button = new()
        {
            Content = CreateFloatingControlShadowHost(
                icon,
                windowIconHostSize,
                floatingControlShadow),
            Focusable = false
        };
        button.Classes.Add("Icon");
        button.Classes.Add("title-action");
        button.Click += clickHandler;
        Avalonia.Controls.Controls controls = [button];

        return controls;
    }

    private static void ConfigureWindowResizeBorders(
        Grid windowResizeOverlay,
        ImageViewerViewEvents events)
    {
        foreach (Border border in windowResizeOverlay.Children.OfType<Border>())
        {
            if (border.Tag is not WindowSizingEdges sizingEdges)
            {
                throw new InvalidOperationException(
                    "A window resize border is missing its sizing edges.");
            }

            border.Cursor = GetWindowResizeCursor(sizingEdges);
            border.PointerPressed += events.WindowResizePointerPressed;
            border.PointerMoved += events.WindowResizePointerMoved;
            border.PointerReleased += events.WindowResizePointerReleased;
        }
    }

    private static Cursor GetWindowResizeCursor(WindowSizingEdges sizingEdges)
    {
        bool includesHorizontalEdge = sizingEdges.IncludesHorizontalEdge();
        bool includesVerticalEdge = sizingEdges.IncludesVerticalEdge();

        if (!includesVerticalEdge)
        {
            return ViewerCursors.HorizontalResize;
        }

        if (!includesHorizontalEdge)
        {
            return ViewerCursors.VerticalResize;
        }

        bool slopesDownRight = sizingEdges.HasFlag(WindowSizingEdges.Top)
            == sizingEdges.HasFlag(WindowSizingEdges.Left);

        return slopesDownRight
            ? ViewerCursors.TopLeftResize
            : ViewerCursors.TopRightResize;
    }

    private static Border CreateContextMenu(
        IReadOnlyList<PicaActionDefinition> actions,
        ImageViewerViewEvents events,
        out Button openWithButton)
    {
        StackPanel panel = new();
        panel.Classes.Add("viewer-menu-items");
        panel.Children.Add(CreateMenuButton(
            "Копировать",
            CopyIconGeometry,
            events.ContextCopyClicked,
            0d));

        panel.Children.Add(CreateMenuButton(
            ViewerUiStrings.SaveAs,
            SaveIconGeometry,
            events.ContextSaveAsClicked,
            0d));
        panel.Children.Add(CreateMenuButton(
            "Выделить область",
            "M6,6 L12,6 L12,8 L8,8 L8,12 L6,12 Z M12,16 L16,16 L16,12 L18,12 L18,18 L12,18 Z",
            events.ContextSelectAreaClicked,
            0d));

        foreach (PicaActionDefinition action in GetActions(actions, PicaActionTargets.CurrentImage))
        {
            Button button = CreateMenuButton(
                action.DisplayName,
                action.IconGeometry,
                events.ContextExternalActionClicked,
                action.IconRotationDegrees);
            button.Tag = action;
            panel.Children.Add(button);
        }

        panel.Children.Add(CreateMenuButton(
            "Показать в папке",
            FolderIconGeometry,
            events.ContextRevealInFolderClicked,
            0d));
        openWithButton = CreateSubmenuButton(
            "Открыть с помощью",
            OpenWithIconGeometry,
            events.ContextOpenWithClicked);
        panel.Children.Add(openWithButton);

        return CreateFloatingMenu(panel);
    }

    private static Border CreateOpenWithMenu(out StackPanel items)
    {
        items = new StackPanel();
        items.Classes.Add("viewer-menu-items");

        return CreateFloatingMenu(items);
    }

    private static Canvas CreateClippedMenuLayer()
    {
        return new Canvas
        {
            ClipToBounds = true
        };
    }

    private static Border CreateFloatingMenu(StackPanel content)
    {
        Border menu = new()
        {
            Child = content,
            IsVisible = false
        };
        menu.Classes.Add("viewer-floating-menu");

        return menu;
    }

    private static Canvas CreateSelectionOverlay(
        IReadOnlyList<PicaActionDefinition> actions,
        ImageViewerViewEvents events,
        IBrush menuForegroundBrush,
        IBrush destructiveIconBrush,
        double hiddenControlsOpacity,
        out ShapePath shade,
        out ShapePath frame,
        out StackPanel toolbar,
        out Button openWithButton)
    {
        Canvas overlay = new()
        {
            Background = Brushes.Transparent,
            IsVisible = false
        };
        shade = new ShapePath
        {
            Fill = new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)),
            IsHitTestVisible = false,
            Opacity = hiddenControlsOpacity,
            Transitions = CreateOpacityTransition(
                ImageViewerVisualMetrics.SelectionOverlayFadeDuration)
        };
        frame = new ShapePath
        {
            Fill = Brushes.Transparent,
            IsHitTestVisible = false,
            Opacity = hiddenControlsOpacity,
            Stroke = new SolidColorBrush(Color.FromArgb(230, 255, 255, 255)),
            StrokeDashArray = [4d, 4d],
            StrokeThickness = 1d,
            Transitions = CreateOpacityTransition(
                ImageViewerVisualMetrics.SelectionOverlayFadeDuration)
        };
        toolbar = new StackPanel
        {
            Height = ImageViewerVisualMetrics.SelectionToolbarHeight,
            Orientation = Orientation.Horizontal,
            Spacing = SelectionButtonSpacing,
            IsVisible = false
        };
        toolbar.Children.Add(CreateSelectionButton(
            CopyIconGeometry,
            events.SelectionCopyClicked,
            0d,
            menuForegroundBrush));

        foreach (PicaActionDefinition action in GetActions(actions, PicaActionTargets.Selection))
        {
            Button button = CreateSelectionButton(
                action.IconGeometry,
                events.SelectionExternalActionClicked,
                action.IconRotationDegrees,
                menuForegroundBrush);
            button.Tag = action;
            toolbar.Children.Add(button);
        }

        toolbar.Children.Add(CreateSelectionButton(
            SaveIconGeometry,
            events.SelectionSaveAsClicked,
            0d,
            menuForegroundBrush));
        openWithButton = CreateSelectionButton(
            OpenWithIconGeometry,
            events.SelectionOpenWithClicked,
            0d,
            menuForegroundBrush);
        toolbar.Children.Add(openWithButton);
        toolbar.Children.Add(CreateSelectionButton(
            ViewerIconGeometries.CloseOrCancel,
            events.SelectionCancelClicked,
            0d,
            destructiveIconBrush));
        toolbar.Width = GetSelectionToolbarWidth(toolbar.Children.Count);
        overlay.Children.Add(shade);
        overlay.Children.Add(frame);
        overlay.Children.Add(toolbar);

        return overlay;
    }

    private static Button CreateMenuButton(
        string text,
        string geometry,
        EventHandler<RoutedEventArgs> clickHandler,
        double iconRotationDegrees)
    {
        StackPanel content = CreateMenuButtonContent(text, geometry, iconRotationDegrees);

        return CreateMenuButton(content, clickHandler);
    }

    private static Button CreateSubmenuButton(
        string text,
        string geometry,
        EventHandler<RoutedEventArgs> clickHandler)
    {
        StackPanel label = CreateMenuButtonContent(text, geometry, 0d);

        return CreateSubmenuButton(label, clickHandler);
    }

    private static Button CreateSubmenuButton(
        string text,
        EventHandler<RoutedEventArgs> clickHandler)
    {
        StackPanel label = CreateMenuButtonPanel();

        label.Children.Add(CreateMenuIconHost(null));
        label.Children.Add(CreateMenuTextBlock(text));

        return CreateSubmenuButton(label, clickHandler);
    }

    private static Button CreateSubmenuButton(
        Control label,
        EventHandler<RoutedEventArgs> clickHandler)
    {
        Grid content = new()
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto")
        };

        content.Children.Add(label);

        PathIcon indicator = CreatePathIcon(
            ViewerIconGeometries.Submenu,
            14d,
            0d);
        indicator.Classes.Add("viewer-menu-icon");

        Grid.SetColumn(indicator, 1);
        content.Children.Add(indicator);

        return CreateMenuButton(content, clickHandler);
    }

    private static Button CreateTextMenuButton(
        string text,
        EventHandler<RoutedEventArgs> clickHandler)
    {
        return CreateMenuButton(CreateMenuTextBlock(text), clickHandler);
    }

    private static Button CreateOpenWithApplicationMenuButton(
        string text,
        Bitmap? icon,
        EventHandler<RoutedEventArgs> clickHandler)
    {
        StackPanel content = CreateMenuButtonPanel();

        if (icon is not null)
        {
            Image applicationIcon = new()
            {
                Source = icon,
                Stretch = Stretch.Uniform
            };
            applicationIcon.Classes.Add("viewer-menu-application-icon");
            content.Children.Add(applicationIcon);
        }

        content.Children.Add(CreateMenuTextBlock(text));

        return CreateMenuButton(content, clickHandler);
    }

    private static Bitmap? CreateOpenWithApplicationIcon(byte[]? pngContent)
    {
        if ((pngContent is null) || (pngContent.Length == 0))
        {
            return null;
        }

        using MemoryStream stream = new(pngContent, writable: false);

        return new Bitmap(stream);
    }

    private static TextBlock CreateMenuTextBlock(string text)
    {
        TextBlock textBlock = new()
        {
            Text = text
        };
        textBlock.Classes.Add("viewer-menu-text");

        return textBlock;
    }

    private static StackPanel CreateMenuButtonPanel()
    {
        StackPanel panel = new();
        panel.Classes.Add("viewer-menu-content");

        return panel;
    }

    private static Grid CreateMenuIconHost(Control? icon)
    {
        Grid host = new();
        host.Classes.Add("viewer-menu-icon-host");

        if (icon is not null)
        {
            host.Children.Add(icon);
        }

        return host;
    }

    private static StackPanel CreateMenuButtonContent(
        string text,
        string geometry,
        double iconRotationDegrees)
    {
        StackPanel content = CreateMenuButtonPanel();
        PathIcon icon = CreatePathIcon(
            geometry,
            iconRotationDegrees);
        icon.Classes.Add("viewer-menu-icon");
        content.Children.Add(icon);
        content.Children.Add(CreateMenuTextBlock(text));

        return content;
    }

    private static Button CreateMenuButton(
        Control content,
        EventHandler<RoutedEventArgs> clickHandler)
    {
        Button button = new()
        {
            Content = content
        };
        button.Classes.Add("viewer-menu-button");
        button.Click += clickHandler;

        return button;
    }

    private static Button CreateSelectionButton(
        string geometry,
        EventHandler<RoutedEventArgs> clickHandler,
        double iconRotationDegrees,
        IBrush iconBrush)
    {
        return CreateSelectionButton(
            StreamGeometry.Parse(geometry),
            clickHandler,
            iconRotationDegrees,
            iconBrush);
    }

    private static Button CreateSelectionButton(
        Geometry geometry,
        EventHandler<RoutedEventArgs> clickHandler,
        double iconRotationDegrees,
        IBrush iconBrush)
    {
        PathIcon icon = CreatePathIcon(
            geometry,
            iconRotationDegrees);
        icon.Classes.Add("viewer-tool-icon");
        icon.Foreground = iconBrush;
        Button button = new()
        {
            Width = SelectionButtonSize,
            Height = SelectionButtonSize,
            Content = icon
        };
        button.Classes.Add("viewer-tool-button");
        button.Click += clickHandler;

        return button;
    }

    private static PathIcon CreatePathIcon(
        string geometry,
        double rotationDegrees)
    {
        return CreatePathIcon(
            StreamGeometry.Parse(geometry),
            rotationDegrees);
    }

    private static PathIcon CreatePathIcon(
        Geometry geometry,
        double rotationDegrees)
    {
        PathIcon icon = new()
        {
            Data = geometry
        };

        if (Math.Abs(rotationDegrees) > double.Epsilon)
        {
            icon.RenderTransform = new RotateTransform(rotationDegrees);
            icon.RenderTransformOrigin = new RelativePoint(0.5d, 0.5d, RelativeUnit.Relative);
        }

        return icon;
    }

    private static PathIcon CreatePathIcon(
        Geometry geometry,
        double size,
        double rotationDegrees)
    {
        PathIcon icon = CreatePathIcon(
            geometry,
            rotationDegrees);
        icon.Width = size;
        icon.Height = size;

        return icon;
    }

    private static Grid CreateFloatingControlShadowHost(
        Control control,
        double hostSize,
        IEffect floatingControlShadow)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(floatingControlShadow);
        Grid host = new()
        {
            Width = hostSize,
            Height = hostSize,
            ClipToBounds = false,
            Effect = floatingControlShadow
        };
        host.Children.Add(control);

        return host;
    }

    private static IReadOnlyList<PicaActionDefinition> GetActions(
        IReadOnlyList<PicaActionDefinition> actions,
        PicaActionTargets target)
    {
        return actions
            .Where(action => (action.Targets & target) == target)
            .OrderBy(action => action.Order)
            .ThenBy(action => action.Id, StringComparer.Ordinal)
            .ToList();
    }

    private static double GetSelectionToolbarWidth(int buttonCount)
    {
        return (buttonCount * SelectionButtonSize)
            + (Math.Max(0, buttonCount - 1) * SelectionButtonSpacing)
            + SelectionToolbarPadding;
    }

    private static Transitions CreateOpacityTransition(TimeSpan duration)
    {
        Transitions transitions =
        [
            new DoubleTransition
            {
                Property = Visual.OpacityProperty,
                Duration = duration
            }
        ];

        return transitions;
    }

    private double NormalizeCheckerboardPatternOffset(double offset)
    {
        return offset % _checkerboardTileSize;
    }

    private void DisposeOpenWithIcons()
    {
        foreach (Bitmap icon in _openWithIcons)
        {
            icon.Dispose();
        }

        _openWithIcons.Clear();
    }

    private IBrush GetRequiredBrush(string resourceKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceKey);

        if (!this.TryFindResource(
            resourceKey,
            ActualThemeVariant,
            out object? resource)
            || resource is not IBrush brush)
        {
            throw new InvalidOperationException(
                $"The image viewer is missing its '{resourceKey}' brush.");
        }

        return brush;
    }

    private IEffect GetRequiredEffect(string resourceKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceKey);

        if (!this.TryFindResource(
            resourceKey,
            ActualThemeVariant,
            out object? resource)
            || resource is not IEffect effect)
        {
            throw new InvalidOperationException(
                $"The image viewer is missing its '{resourceKey}' effect.");
        }

        return effect;
    }

    private double GetRequiredDouble(string resourceKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceKey);

        if (!this.TryFindResource(
            resourceKey,
            ActualThemeVariant,
            out object? resource)
            || resource is not double value)
        {
            throw new InvalidOperationException(
                $"The image viewer is missing its '{resourceKey}' number.");
        }

        return value;
    }

    private Button GetRequiredButton(string name)
    {
        return this.FindControl<Button>(name)
            ?? throw new InvalidOperationException(
                $"The image viewer is missing its '{name}' button.");
    }

    private PathIcon GetRequiredPathIcon(string name)
    {
        return this.FindControl<PathIcon>(name)
            ?? throw new InvalidOperationException(
                $"The image viewer is missing its '{name}' path icon.");
    }

    private void Compose()
    {
        ContextMenuLayer.Children.Add(ViewerContextMenu);
        _viewerDynamicLayer.Children.Add(ContextMenuLayer);
        _viewerDynamicLayer.Children.Add(SelectionOverlay);
        OpenWithMenuLayer.Children.Add(OpenWithMenu);
        _viewerDynamicLayer.Children.Add(OpenWithMenuLayer);
        _viewerDynamicLayer.Children.Add(_toolMenuControl);
        Root.Children.Add(SettingsPanel);
    }
}
