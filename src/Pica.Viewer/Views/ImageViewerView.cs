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

using ShapePath = Avalonia.Controls.Shapes.Path;

namespace Pica.Viewer.Views;

internal sealed class ImageViewerView : IDisposable
{
    internal Grid Root { get; }
    internal Grid ViewerArea { get; }
    internal Grid WindowResizeOverlay { get; }
    internal ViewerSettingsPanel SettingsPanel { get; }
    internal Border FadeOverlay { get; }
    internal Canvas ImageCanvas { get; }
    internal Image Image { get; }
    internal Border LeftNavigationArea { get; }
    internal Border RightNavigationArea { get; }
    internal PixelFilteringToggleSwitch FilteringToggle { get; }
    internal StackPanel BottomControls { get; }
    internal Border ImageInformationPanel { get; }
    internal TextBlock ImageInformationText { get; }
    internal Button FullscreenSettingsButton { get; }
    internal Button WindowModeButton { get; }
    internal Button CloseButton { get; }
    internal Canvas ContextMenuLayer { get; }
    internal Border ContextMenu { get; }
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

    private const string CopyIconGeometry = "M8,7 L17,7 L17,19 L8,19 Z M6,5 L15,5 L15,3 L4,3 L4,15 L6,15 Z";
    private const string SaveIconGeometry = "M5,3 L16,3 L21,8 L21,19 L19,21 L5,21 L3,19 L3,5 Z M7,6 L7,10 L11,10 L11,8 L13,8 L13,10 L16,10 L16,6 Z M7,19 L17,19 L17,14 L7,14 Z";
    private const string FolderIconGeometry = "M3,6 L10,6 L12,8 L21,8 L21,19 L3,19 Z";
    private const string OpenWithIconGeometry = "M13,3 L20,3 L20,10 L18,10 L18,6.4 L9.4,15 L8,13.6 L16.6,5 L13,5 Z M4,5 L10,5 L10,7 L6,7 L6,17 L16,17 L16,13 L18,13 L18,19 L4,19 Z";
    private const string CloseOrCancelIconGeometry = "M6,7.4 L7.4,6 L12,10.6 L16.6,6 L18,7.4 L13.4,12 L18,16.6 L16.6,18 L12,13.4 L7.4,18 L6,16.6 L10.6,12 Z";
    private const string SubmenuIconGeometry = "M9,6 L15,12 L9,18 Z";
    private const string WindowModeIconGeometry = "M6,6 L18,6 L18,18 L6,18 Z M8,8 L8,16 L16,16 L16,8 Z";
    private const string SettingsIconClassName = "settings-icon";
    private const double SettingsPanelTopGap = 8d;
    private const double SettingsPanelRightMargin = 12d;
    private const double NavigationIconSize = 44d;
    private const double NavigationShadowPadding = 8d;
    private const double ToolShadowPadding = 7d;
    private const double ToolButtonSize = 44d;
    private const double ToolIconSize = 22d;
    private const double WindowResizeBorderSize = 6d;
    private const double WindowResizeCornerSize = 12d;
    private const double SelectionButtonSize = 42d;
    private const double SelectionButtonSpacing = 6d;
    private const double SelectionToolbarPadding = 8d;
    private const double ControlsFadeDurationSeconds = 0.16d;
    private static readonly TimeSpan ControlsFadeDuration =
        TimeSpan.FromSeconds(ControlsFadeDurationSeconds);
    private static readonly IBrush DestructiveIconBrush =
        new SolidColorBrush(Color.FromRgb(179, 38, 30));
    private readonly List<Bitmap> _openWithIcons = [];

    internal ImageViewerView(
        bool isFilteringEnabled,
        IReadOnlyList<ViewerSettingControl> settingControls,
        IReadOnlyList<PicaActionDefinition> actions,
        ViewerWindowMode windowMode,
        ImageViewerViewEvents events)
    {
        ArgumentNullException.ThrowIfNull(settingControls);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(events);

        Root = CreateRoot();
        ViewerArea = CreateViewerArea();
        WindowResizeOverlay = CreateWindowResizeOverlay(windowMode, events);
        SettingsPanel = CreateSettingsPanel(settingControls, windowMode);
        FadeOverlay = CreateFadeOverlay();
        ImageCanvas = CreateImageCanvas();
        Image = CreateImage();
        LeftNavigationArea = CreateNavigationArea(
            HorizontalAlignment.Left,
            "M15,4 L7,12 L15,20 Z");
        RightNavigationArea = CreateNavigationArea(
            HorizontalAlignment.Right,
            "M9,4 L17,12 L9,20 Z");
        FilteringToggle = new PixelFilteringToggleSwitch
        {
            IsFilteringEnabled = isFilteringEnabled
        };
        BottomControls = CreateBottomControls(FilteringToggle, events);
        ImageInformationPanel = CreateImageInformationPanel(
            windowMode,
            out TextBlock imageInformationText);
        ImageInformationText = imageInformationText;
        FullscreenSettingsButton = CreateFullscreenSettingsButton(events.SettingsClicked);
        WindowModeButton = CreateWindowModeButton(events.WindowModeClicked);
        CloseButton = CreateCloseButton(events.CloseClicked);
        ContextMenuLayer = CreateClippedMenuLayer();
        ContextMenu = CreateContextMenu(actions, events, out Button contextOpenWithButton);
        ContextOpenWithButton = contextOpenWithButton;
        OpenWithMenuLayer = CreateClippedMenuLayer();
        OpenWithMenu = CreateOpenWithMenu(out StackPanel openWithMenuItems);
        OpenWithMenuItems = openWithMenuItems;
        SelectionOverlay = CreateSelectionOverlay(
            actions,
            events,
            out ShapePath selectionShade,
            out ShapePath selectionFrame,
            out StackPanel selectionToolbar,
            out Button selectionOpenWithButton);
        SelectionShade = selectionShade;
        SelectionFrame = selectionFrame;
        SelectionToolbar = selectionToolbar;
        SelectionOpenWithButton = selectionOpenWithButton;
        TitleBarSettingsControls = CreateTitleBarSettingsButton(events.SettingsClicked);

        Compose();
    }

    public void Dispose()
    {
        DisposeOpenWithIcons();
    }

    internal void UpdateSettingsPanelPlacement(ViewerWindowMode windowMode)
    {
        SettingsPanel.Margin = CreateSettingsPanelMargin(windowMode);
    }

    internal void ApplyImageFiltering(bool isFilteringEnabled)
    {
        RenderOptions.SetBitmapInterpolationMode(
            Image,
            isFilteringEnabled
                ? BitmapInterpolationMode.HighQuality
                : BitmapInterpolationMode.None);
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

    private static Grid CreateRoot()
    {
        return new Grid
        {
            Background = Brushes.Black,
            ClipToBounds = true,
            Opacity = ImageViewerVisualMetrics.VisibleControlsOpacity
        };
    }

    private static Grid CreateViewerArea()
    {
        return new Grid
        {
            Background = Brushes.Black,
            ClipToBounds = true
        };
    }

    private static Grid CreateWindowResizeOverlay(
        ViewerWindowMode windowMode,
        ImageViewerViewEvents events)
    {
        Grid overlay = new()
        {
            IsVisible = windowMode == ViewerWindowMode.Windowed
        };
        overlay.Children.Add(CreateWindowResizeBorder(
            WindowSizingEdges.Left,
            WindowResizeBorderSize,
            double.NaN,
            HorizontalAlignment.Left,
            VerticalAlignment.Stretch,
            events));
        overlay.Children.Add(CreateWindowResizeBorder(
            WindowSizingEdges.Right,
            WindowResizeBorderSize,
            double.NaN,
            HorizontalAlignment.Right,
            VerticalAlignment.Stretch,
            events));
        overlay.Children.Add(CreateWindowResizeBorder(
            WindowSizingEdges.Top,
            double.NaN,
            WindowResizeBorderSize,
            HorizontalAlignment.Stretch,
            VerticalAlignment.Top,
            events));
        overlay.Children.Add(CreateWindowResizeBorder(
            WindowSizingEdges.Bottom,
            double.NaN,
            WindowResizeBorderSize,
            HorizontalAlignment.Stretch,
            VerticalAlignment.Bottom,
            events));
        overlay.Children.Add(CreateWindowResizeBorder(
            WindowSizingEdges.TopLeft,
            WindowResizeCornerSize,
            WindowResizeCornerSize,
            HorizontalAlignment.Left,
            VerticalAlignment.Top,
            events));
        overlay.Children.Add(CreateWindowResizeBorder(
            WindowSizingEdges.TopRight,
            WindowResizeCornerSize,
            WindowResizeCornerSize,
            HorizontalAlignment.Right,
            VerticalAlignment.Top,
            events));
        overlay.Children.Add(CreateWindowResizeBorder(
            WindowSizingEdges.BottomLeft,
            WindowResizeCornerSize,
            WindowResizeCornerSize,
            HorizontalAlignment.Left,
            VerticalAlignment.Bottom,
            events));
        overlay.Children.Add(CreateWindowResizeBorder(
            WindowSizingEdges.BottomRight,
            WindowResizeCornerSize,
            WindowResizeCornerSize,
            HorizontalAlignment.Right,
            VerticalAlignment.Bottom,
            events));

        return overlay;
    }

    private static ViewerSettingsPanel CreateSettingsPanel(
        IReadOnlyList<ViewerSettingControl> settingControls,
        ViewerWindowMode windowMode)
    {
        return new ViewerSettingsPanel(settingControls)
        {
            Margin = CreateSettingsPanelMargin(windowMode),
            HorizontalAlignment = HorizontalAlignment.Right,
            IsHitTestVisible = false,
            IsVisible = false,
            Opacity = ImageViewerVisualMetrics.HiddenControlsOpacity,
            RenderTransform = new TranslateTransform(
                0d,
                ImageViewerVisualMetrics.SettingsPanelHiddenOffset),
            VerticalAlignment = VerticalAlignment.Top
        };
    }

    private static Thickness CreateSettingsPanelMargin(ViewerWindowMode windowMode)
    {
        double topMargin = windowMode == ViewerWindowMode.Windowed
            ? SettingsPanelTopGap
            : ImageViewerVisualMetrics.CloseRevealSize + SettingsPanelTopGap;

        return new Thickness(0d, topMargin, SettingsPanelRightMargin, 0d);
    }

    private static Avalonia.Controls.Controls CreateTitleBarSettingsButton(
        EventHandler<RoutedEventArgs> clickHandler)
    {
        PathIcon icon = new();
        icon.Classes.Add(SettingsIconClassName);
        Button button = new()
        {
            Content = CreateFloatingControlShadowHost(icon, ToolIconSize, ToolShadowPadding),
            Focusable = false
        };
        button.Classes.Add("Icon");
        button.Classes.Add("title-action");
        button.Click += clickHandler;
        Avalonia.Controls.Controls controls = [button];

        return controls;
    }

    private static Border CreateWindowResizeBorder(
        WindowSizingEdges sizingEdges,
        double width,
        double height,
        HorizontalAlignment horizontalAlignment,
        VerticalAlignment verticalAlignment,
        ImageViewerViewEvents events)
    {
        Border border = new()
        {
            Width = width,
            Height = height,
            Background = Brushes.Transparent,
            Cursor = GetWindowResizeCursor(sizingEdges),
            HorizontalAlignment = horizontalAlignment,
            Tag = sizingEdges,
            VerticalAlignment = verticalAlignment
        };
        border.PointerPressed += events.WindowResizePointerPressed;
        border.PointerMoved += events.WindowResizePointerMoved;
        border.PointerReleased += events.WindowResizePointerReleased;

        return border;
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

    private static Border CreateFadeOverlay()
    {
        return new Border
        {
            Background = Brushes.Black,
            IsHitTestVisible = false,
            Opacity = ImageViewerVisualMetrics.VisibleControlsOpacity
        };
    }

    private static Canvas CreateImageCanvas()
    {
        return new Canvas
        {
            Background = Brushes.Transparent,
            ClipToBounds = false
        };
    }

    private static Image CreateImage()
    {
        return new Image
        {
            Stretch = Stretch.Fill
        };
    }

    private static Border CreateNavigationArea(HorizontalAlignment alignment, string geometry)
    {
        PathIcon icon = new()
        {
            Width = NavigationIconSize,
            Height = NavigationIconSize,
            ClipToBounds = false,
            Data = StreamGeometry.Parse(geometry),
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid iconHost = CreateFloatingControlShadowHost(
            icon,
            NavigationIconSize,
            NavigationShadowPadding);

        return new Border
        {
            Width = ImageViewerVisualMetrics.ArrowAreaMinWidth,
            HorizontalAlignment = alignment,
            VerticalAlignment = VerticalAlignment.Stretch,
            Background = Brushes.Transparent,
            Child = iconHost,
            Cursor = ViewerCursors.Hand,
            Opacity = ImageViewerVisualMetrics.HiddenControlsOpacity,
            Transitions = CreateOpacityTransition(ControlsFadeDuration)
        };
    }

    private static StackPanel CreateBottomControls(
        PixelFilteringToggleSwitch filteringToggle,
        ImageViewerViewEvents events)
    {
        StackPanel controls = new()
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0d, 0d, 0d, 26d),
            Orientation = Orientation.Horizontal,
            Spacing = 8d,
            Opacity = ImageViewerVisualMetrics.HiddenControlsOpacity,
            Transitions = CreateOpacityTransition(ControlsFadeDuration)
        };
        controls.Children.Add(CreateIconButton(
            "M5,11 L19,11 L19,13 L5,13 Z",
            events.ZoomOutClicked,
            0d,
            Brushes.White));
        controls.Children.Add(CreateIconButton(
            "M12,7 A5,5 0 1 0 12,17 A5,5 0 1 0 12,7",
            events.ResetClicked,
            0d,
            Brushes.White));
        controls.Children.Add(CreateIconButton(
            "M11,5 L13,5 L13,11 L19,11 L19,13 L13,13 L13,19 L11,19 L11,13 L5,13 L5,11 L11,11 Z",
            events.ZoomInClicked,
            0d,
            Brushes.White));
        controls.Children.Add(filteringToggle);

        return controls;
    }

    private static Border CreateImageInformationPanel(
        ViewerWindowMode windowMode,
        out TextBlock informationText)
    {
        informationText = new TextBlock
        {
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap
        };

        return new Border
        {
            Margin = new Thickness(ImageViewerVisualMetrics.InformationPanelMargin),
            Padding = new Thickness(12d, 8d),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Background = new SolidColorBrush(Color.FromArgb(192, 24, 24, 24)),
            Child = informationText,
            CornerRadius = new CornerRadius(8d),
            IsHitTestVisible = false,
            IsVisible = windowMode == ViewerWindowMode.FullScreen,
            Opacity = ImageViewerVisualMetrics.HiddenControlsOpacity,
            Transitions = CreateOpacityTransition(ControlsFadeDuration)
        };
    }

    private static Button CreateCloseButton(EventHandler<RoutedEventArgs> clickHandler)
    {
        Button button = CreateIconButton(
            CloseOrCancelIconGeometry,
            clickHandler,
            0d,
            DestructiveIconBrush);
        button.HorizontalContentAlignment = HorizontalAlignment.Center;
        button.Padding = new Thickness(0d);
        button.VerticalContentAlignment = VerticalAlignment.Center;
        ConfigureWindowCornerButton(button, 0d);

        return button;
    }

    private static Button CreateWindowModeButton(EventHandler<RoutedEventArgs> clickHandler)
    {
        Button button = CreateIconButton(
            WindowModeIconGeometry,
            clickHandler,
            0d,
            Brushes.White);
        ConfigureWindowCornerButton(button, ImageViewerVisualMetrics.CloseRevealSize);

        return button;
    }

    private static void ConfigureWindowCornerButton(Button button, double rightMargin)
    {
        button.HorizontalAlignment = HorizontalAlignment.Right;
        button.VerticalAlignment = VerticalAlignment.Top;
        button.Margin = new Thickness(
            0d,
            0d,
            rightMargin,
            0d);
        button.Width = ImageViewerVisualMetrics.CloseRevealSize;
        button.Height = ImageViewerVisualMetrics.CloseRevealSize;
        button.Background = Brushes.Transparent;
        button.BorderBrush = Brushes.Transparent;
        button.BorderThickness = new Thickness(0d);
        button.CornerRadius = new CornerRadius(0d);
        button.Opacity = ImageViewerVisualMetrics.HiddenControlsOpacity;
        button.Transitions = CreateOpacityTransition(ControlsFadeDuration);
        WrapButtonContentWithFloatingShadow(button);
    }

    private static Button CreateFullscreenSettingsButton(EventHandler<RoutedEventArgs> clickHandler)
    {
        PathIcon icon = new()
        {
            Width = ToolIconSize,
            Height = ToolIconSize,
            Foreground = Brushes.White
        };
        icon.Classes.Add(SettingsIconClassName);
        Button button = new()
        {
            Width = ImageViewerVisualMetrics.CloseRevealSize,
            Height = ImageViewerVisualMetrics.CloseRevealSize,
            Margin = new Thickness(
                0d,
                0d,
                ImageViewerVisualMetrics.CloseRevealSize * 2d,
                0d),
            Padding = new Thickness(0d),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0d),
            Content = CreateFloatingControlShadowHost(icon, ToolIconSize, ToolShadowPadding),
            CornerRadius = new CornerRadius(0d),
            Cursor = ViewerCursors.Hand,
            Focusable = false,
            Opacity = ImageViewerVisualMetrics.HiddenControlsOpacity,
            Transitions = CreateOpacityTransition(ControlsFadeDuration)
        };
        button.Click += clickHandler;

        return button;
    }

    private static Border CreateContextMenu(
        IReadOnlyList<PicaActionDefinition> actions,
        ImageViewerViewEvents events,
        out Button openWithButton)
    {
        StackPanel panel = new()
        {
            Orientation = Orientation.Vertical,
            Spacing = 2d
        };
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
        items = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 2d
        };

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
        return new Border
        {
            Padding = new Thickness(6d),
            Background = new SolidColorBrush(Color.FromArgb(232, 24, 24, 24)),
            CornerRadius = new CornerRadius(8d),
            Child = content,
            HorizontalAlignment = HorizontalAlignment.Left,
            IsVisible = false,
            Opacity = ImageViewerVisualMetrics.HiddenControlsOpacity,
            Transitions = CreateOpacityTransition(ControlsFadeDuration),
            VerticalAlignment = VerticalAlignment.Top
        };
    }

    private static Canvas CreateSelectionOverlay(
        IReadOnlyList<PicaActionDefinition> actions,
        ImageViewerViewEvents events,
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
            Opacity = ImageViewerVisualMetrics.HiddenControlsOpacity,
            Transitions = CreateOpacityTransition(
                ImageViewerVisualMetrics.SelectionOverlayFadeDuration)
        };
        frame = new ShapePath
        {
            Fill = Brushes.Transparent,
            IsHitTestVisible = false,
            Opacity = ImageViewerVisualMetrics.HiddenControlsOpacity,
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
            Brushes.White));

        foreach (PicaActionDefinition action in GetActions(actions, PicaActionTargets.Selection))
        {
            Button button = CreateSelectionButton(
                action.IconGeometry,
                events.SelectionExternalActionClicked,
                action.IconRotationDegrees,
                Brushes.White);
            button.Tag = action;
            toolbar.Children.Add(button);
        }

        toolbar.Children.Add(CreateSelectionButton(
            SaveIconGeometry,
            events.SelectionSaveAsClicked,
            0d,
            Brushes.White));
        openWithButton = CreateSelectionButton(
            OpenWithIconGeometry,
            events.SelectionOpenWithClicked,
            0d,
            Brushes.White);
        toolbar.Children.Add(openWithButton);
        toolbar.Children.Add(CreateSelectionButton(
            CloseOrCancelIconGeometry,
            events.SelectionCancelClicked,
            0d,
            DestructiveIconBrush));
        toolbar.Width = GetSelectionToolbarWidth(toolbar.Children.Count);
        overlay.Children.Add(shade);
        overlay.Children.Add(frame);
        overlay.Children.Add(toolbar);

        return overlay;
    }

    private static Button CreateIconButton(
        string geometry,
        EventHandler<RoutedEventArgs> clickHandler,
        double iconRotationDegrees,
        IBrush iconBrush)
    {
        Button button = new()
        {
            Width = ToolButtonSize,
            Height = ToolButtonSize,
            MinWidth = 0d,
            MinHeight = 0d,
            Cursor = ViewerCursors.Hand,
            Focusable = false,
            Padding = new Thickness(0d),
            Background = new SolidColorBrush(Color.FromArgb(150, 16, 16, 16)),
            BorderBrush = Brushes.Transparent,
            CornerRadius = new CornerRadius(8d),
            Content = CreatePathIcon(geometry, ToolIconSize, iconRotationDegrees, iconBrush)
        };
        button.Click += clickHandler;

        return button;
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
        Grid content = new()
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto")
        };
        StackPanel label = CreateMenuButtonContent(text, geometry, 0d);
        content.Children.Add(label);
        PathIcon indicator = CreatePathIcon(
            SubmenuIconGeometry,
            14d,
            0d,
            Brushes.White);
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
            content.Children.Add(new Image
            {
                Width = 18d,
                Height = 18d,
                Source = icon,
                Stretch = Stretch.Uniform
            });
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
        return new TextBlock
        {
            Foreground = Brushes.White,
            Text = text,
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    private static StackPanel CreateMenuButtonPanel()
    {
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8d
        };
    }

    private static StackPanel CreateMenuButtonContent(
        string text,
        string geometry,
        double iconRotationDegrees)
    {
        StackPanel content = CreateMenuButtonPanel();
        content.Children.Add(CreatePathIcon(
            geometry,
            18d,
            iconRotationDegrees,
            Brushes.White));
        content.Children.Add(CreateMenuTextBlock(text));

        return content;
    }

    private static Button CreateMenuButton(
        Control content,
        EventHandler<RoutedEventArgs> clickHandler)
    {
        Button button = new()
        {
            MinWidth = 148d,
            Cursor = ViewerCursors.Hand,
            Focusable = false,
            Padding = new Thickness(10d, 8d),
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            Content = content
        };
        button.Click += clickHandler;

        return button;
    }

    private static Button CreateSelectionButton(
        string geometry,
        EventHandler<RoutedEventArgs> clickHandler,
        double iconRotationDegrees,
        IBrush iconBrush)
    {
        Button button = CreateIconButton(
            geometry,
            clickHandler,
            iconRotationDegrees,
            iconBrush);
        button.Width = SelectionButtonSize;
        button.Height = SelectionButtonSize;

        return button;
    }

    private static PathIcon CreatePathIcon(
        string geometry,
        double size,
        double rotationDegrees,
        IBrush foreground)
    {
        PathIcon icon = new()
        {
            Width = size,
            Height = size,
            Data = StreamGeometry.Parse(geometry),
            Foreground = foreground
        };

        if (Math.Abs(rotationDegrees) > double.Epsilon)
        {
            icon.RenderTransform = new RotateTransform(rotationDegrees);
            icon.RenderTransformOrigin = new RelativePoint(0.5d, 0.5d, RelativeUnit.Relative);
        }

        return icon;
    }

    private static Grid CreateFloatingControlShadowHost(
        Control control,
        double contentSize,
        double shadowPadding)
    {
        ArgumentNullException.ThrowIfNull(control);
        Grid host = new()
        {
            Width = contentSize + (shadowPadding * 2d),
            Height = contentSize + (shadowPadding * 2d),
            ClipToBounds = false,
            Effect = new DropShadowEffect
            {
                BlurRadius = 6d,
                Color = Colors.Black,
                OffsetX = 0d,
                OffsetY = 1d,
                Opacity = 0.7d
            }
        };
        host.Children.Add(control);

        return host;
    }

    private static void WrapButtonContentWithFloatingShadow(Button button)
    {
        ArgumentNullException.ThrowIfNull(button);

        if (button.Content is not Control content)
        {
            return;
        }

        button.Content = null;
        button.Content = CreateFloatingControlShadowHost(
            content,
            ToolIconSize,
            ToolShadowPadding);
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

    private void DisposeOpenWithIcons()
    {
        foreach (Bitmap icon in _openWithIcons)
        {
            icon.Dispose();
        }

        _openWithIcons.Clear();
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

    private void Compose()
    {
        ImageCanvas.Children.Add(Image);
        ViewerArea.Children.Add(ImageCanvas);
        ViewerArea.Children.Add(LeftNavigationArea);
        ViewerArea.Children.Add(RightNavigationArea);
        ViewerArea.Children.Add(BottomControls);
        ViewerArea.Children.Add(ImageInformationPanel);
        ViewerArea.Children.Add(FullscreenSettingsButton);
        ViewerArea.Children.Add(WindowModeButton);
        ViewerArea.Children.Add(CloseButton);
        ContextMenuLayer.Children.Add(ContextMenu);
        ViewerArea.Children.Add(ContextMenuLayer);
        ViewerArea.Children.Add(SelectionOverlay);
        OpenWithMenuLayer.Children.Add(OpenWithMenu);
        ViewerArea.Children.Add(OpenWithMenuLayer);
        ViewerArea.Children.Add(FadeOverlay);
        Root.Children.Add(ViewerArea);
        Root.Children.Add(WindowResizeOverlay);
        Root.Children.Add(SettingsPanel);
    }
}
