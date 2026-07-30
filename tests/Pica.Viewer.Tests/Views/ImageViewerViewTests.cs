using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using FluentAssertions;
using SkiaSharp;
using Xunit;

using Pica.Protocol;
using Pica.Tests.Common;
using Pica.Viewer.Controls;
using Pica.Viewer.Services;
using Pica.Viewer.Tests.TestDoubles;
using Pica.Viewer.ViewModels;
using Pica.Viewer.Views;

using AvaloniaBitmap = Avalonia.Media.Imaging.Bitmap;

namespace Pica.Viewer.Tests.Views;

[Collection(AvaloniaHeadlessCollection.Name)]
public sealed class ImageViewerViewTests
{
    private static readonly SemaphoreSlim SessionLock = new(1, 1);

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder
            .Configure<Application>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }

    [Fact]
    public async Task Constructor_WithCheckerboardBackground_CreatesTiledLayerBehindImage()
    {
        await DispatchAsync(() =>
        {
            ImageViewerSessionViewModel session = CreateSession(
                false,
                new List<PicaActionDefinition>());
            using ImageViewerView view = new(
                session,
                CreateToolMenu(session, false),
                new List<ViewerSettingControl>(),
                ViewerWindowMode.FullScreen,
                CreateEvents());

            VisualBrush checkerboardBrush = view
                .CheckerboardPattern
                .Background
                .Should()
                .BeOfType<VisualBrush>()
                .Subject;
            RelativeRect expectedDestinationRect = new(
                0d,
                0d,
                10d,
                10d,
                RelativeUnit.Absolute);
            Grid checkerboardTile = checkerboardBrush
                .Visual
                .Should()
                .BeOfType<Grid>()
                .Subject;
            SolidColorBrush lightBrush = checkerboardTile
                .Background
                .Should()
                .BeOfType<SolidColorBrush>()
                .Subject;
            List<SolidColorBrush> darkBrushes = checkerboardTile
                .Children
                .OfType<Border>()
                .Select(border => border.Background)
                .OfType<SolidColorBrush>()
                .ToList();
            TranslateTransform checkerboardTransform =
                view.CheckerboardPattern
                    .RenderTransform
                    .Should()
                    .BeOfType<TranslateTransform>()
                    .Subject;
            const double patternOffsetX = 17d;
            const double patternOffsetY = -9d;
            const double expectedPatternOffsetX = 7d;

            view.UpdateCheckerboardPatternOffset(
                patternOffsetX,
                patternOffsetY);

            view.ImageCanvas.Children.IndexOf(
                    view.CheckerboardBackground)
                .Should()
                .BeLessThan(view.ImageCanvas.Children.IndexOf(view.Image));
            view.CheckerboardBackground.IsVisible.Should().BeFalse();
            view.CheckerboardBackground.ClipToBounds.Should().BeTrue();
            view.CheckerboardBackground.Child.Should().BeSameAs(
                view.CheckerboardPattern);
            view.CheckerboardPattern.Margin.Should().Be(
                new Thickness(-10d));
            checkerboardBrush.DestinationRect.Should().Be(
                expectedDestinationRect);
            checkerboardBrush.TileMode.Should().Be(TileMode.Tile);
            lightBrush.Color.Should().Be(Color.Parse("#FFD5D9DE"));
            darkBrushes.Should().HaveCount(2);
            darkBrushes.Should().OnlyContain(
                brush => brush.Color == Color.Parse("#FFB4BAC2"));
            view.CheckerboardPattern.RenderTransformOrigin.Should().Be(
                RelativePoint.TopLeft);
            checkerboardTransform.X.Should().Be(expectedPatternOffsetX);
            checkerboardTransform.Y.Should().Be(patternOffsetY);
        });
    }

    [Fact]
    public async Task UpdateCheckerboardPatternOffset_WhenBackgroundMoves_KeepsRenderedPatternFixed()
    {
        await DispatchAsync(() =>
        {
            ImageViewerSessionViewModel session = CreateSession(
                false,
                new List<PicaActionDefinition>());
            ImageViewerView view = new(
                session,
                CreateToolMenu(session, false),
                new List<ViewerSettingControl>(),
                ViewerWindowMode.FullScreen,
                CreateEvents());
            Window window = new()
            {
                Width = 160d,
                Height = 120d,
                Content = view
            };

            try
            {
                view.CheckerboardBackground.Width = 80d;
                view.CheckerboardBackground.Height = 80d;
                view.CheckerboardBackground.IsVisible = true;
                view.FadeOverlay.IsVisible = false;
                Canvas.SetLeft(view.CheckerboardBackground, 20d);
                Canvas.SetTop(view.CheckerboardBackground, 20d);
                window.Show();
                using AvaloniaBitmap beforeMove =
                    window.CaptureRenderedFrame()
                    ?? throw new InvalidOperationException(
                        "The checkerboard frame was not rendered.");

                Canvas.SetLeft(view.CheckerboardBackground, 23d);
                view.UpdateCheckerboardPatternOffset(-3d, 0d);
                using AvaloniaBitmap afterMove =
                    window.CaptureRenderedFrame()
                    ?? throw new InvalidOperationException(
                        "The moved checkerboard frame was not rendered.");

                AssertRenderedRegionEqual(
                    beforeMove,
                    afterMove,
                    new PixelRect(30, 30, 60, 60));
            }
            finally
            {
                window.Close();
                view.Dispose();
            }
        });
    }

    [Fact]
    public async Task Constructor_WithOrderedCurrentImageActions_PlacesLastActionBeforeRevealInFolder()
    {
        await DispatchAsync(() =>
        {
            PicaActionDefinition attachAction = new(
                "attach",
                "Прикрепить",
                "M0,0 L1,1",
                0d,
                PicaActionTargets.CurrentImage,
                100);
            PicaActionDefinition showInGalleryAction = new(
                "show-in-gallery",
                "Показать в галерее",
                "M0,0 L1,1",
                0d,
                PicaActionTargets.CurrentImage,
                110);
            List<PicaActionDefinition> actions =
            [
                showInGalleryAction,
                attachAction
            ];
            ImageViewerSessionViewModel session = CreateSession(
                false,
                actions);

            using ImageViewerView view = new(
                session,
                CreateToolMenu(session, false),
                new List<ViewerSettingControl>(),
                ViewerWindowMode.FullScreen,
                CreateEvents());

            StackPanel menuItems = view.ViewerContextMenu
                .Child
                .Should()
                .BeOfType<StackPanel>()
                .Subject;
            List<Button> buttons = menuItems.Children.OfType<Button>().ToList();
            int showInGalleryIndex = buttons.FindIndex(button =>
                object.ReferenceEquals(button.Tag, showInGalleryAction));
            int revealInFolderIndex = buttons.FindIndex(button =>
                string.Equals(
                    GetMenuButtonText(button),
                    "Показать в папке",
                    StringComparison.Ordinal));

            showInGalleryIndex.Should().Be(revealInFolderIndex - 1);
        });
    }

    [Fact]
    public async Task Constructor_WithToolMenuButton_KeepsThreeZoomButtonsCentered()
    {
        await DispatchAsync(() =>
        {
            ImageViewerSessionViewModel session = CreateSession(
                false,
                new List<PicaActionDefinition>());
            using ImageViewerView view = new(
                session,
                CreateToolMenu(session, false),
                new List<ViewerSettingControl>(),
                ViewerWindowMode.FullScreen,
                CreateEvents());

            StackPanel centeredControls = view.BottomControls
                .Children
                .OfType<StackPanel>()
                .Single();

            view.BottomControls.Measure(new Size(1000d, 44d));
            view.BottomControls.Arrange(new Rect(0d, 0d, 1000d, 44d));

            centeredControls.HorizontalAlignment.Should().Be(HorizontalAlignment.Center);
            centeredControls.Children.Should().HaveCount(3);
            centeredControls.Bounds.Center.X.Should().Be(500d);
            view.ToolMenuButton.Bounds.Left
                .Should()
                .Be(centeredControls.Bounds.Right + 8d);
        });
    }

    [Fact]
    public async Task Constructor_WithExistingViewerChrome_PreservesDimensions()
    {
        await DispatchAsync(() =>
        {
            ImageViewerSessionViewModel session = CreateSession(
                false,
                new List<PicaActionDefinition>());

            using ImageViewerView view = new(
                session,
                CreateToolMenu(session, false),
                new List<ViewerSettingControl>(),
                ViewerWindowMode.FullScreen,
                CreateEvents());

            Grid navigationIconHost = view.LeftNavigationArea
                .Child
                .Should()
                .BeOfType<Grid>()
                .Subject;
            PathIcon navigationIcon = navigationIconHost
                .Children
                .Should()
                .ContainSingle()
                .Which
                .Should()
                .BeOfType<PathIcon>()
                .Subject;
            Panel viewerChrome = view.BottomControls
                .Parent
                .Should()
                .BeAssignableTo<Panel>()
                .Subject;

            view.LeftNavigationArea.Width.Should().Be(24d);
            navigationIconHost.Width.Should().Be(60d);
            navigationIconHost.Height.Should().Be(60d);
            navigationIcon.Width.Should().Be(44d);
            navigationIcon.Height.Should().Be(44d);
            view.BottomControls.Height.Should().Be(44d);
            view.ToolMenuButton.Width.Should().Be(44d);
            view.ToolMenuButton.Height.Should().Be(44d);
            view.ImageInformationPanel.Margin.Should().Be(new Thickness(16d));
            view.FullscreenSettingsButton.Margin.Right.Should().Be(128d);
            view.WindowModeButton.Margin.Right.Should().Be(64d);
            view.CloseButton.Width.Should().Be(64d);
            view.CloseButton.Height.Should().Be(64d);
            viewerChrome.Children.IndexOf(view.LeftNavigationArea).Should().Be(0);
            viewerChrome.Children.IndexOf(view.RightNavigationArea).Should().Be(1);
            viewerChrome.Children.IndexOf(view.BottomControls).Should().Be(2);
            viewerChrome.Children.IndexOf(view.ImageInformationPanel).Should().Be(3);
            viewerChrome.Children.IndexOf(view.FullscreenSettingsButton).Should().Be(4);
            viewerChrome.Children.IndexOf(view.WindowModeButton).Should().Be(5);
            viewerChrome.Children.IndexOf(view.CloseButton).Should().Be(6);
        });
    }

    [Fact]
    public async Task Constructor_WithInformationPanelMarginResource_UsesThicknessCompatibleWithMargin()
    {
        await DispatchAsync(() =>
        {
            ImageViewerSessionViewModel session = CreateSession(
                false,
                new List<PicaActionDefinition>());
            using ImageViewerView view = new(
                session,
                CreateToolMenu(session, false),
                new List<ViewerSettingControl>(),
                ViewerWindowMode.FullScreen,
                CreateEvents());

            bool resourceFound = view.TryFindResource(
                "ViewerInformationPanelMargin",
                view.ActualThemeVariant,
                out object? resource);

            resourceFound.Should().BeTrue();
            resource.Should().BeOfType<Thickness>();
            view.ImageInformationPanel.Margin.Should().Be(new Thickness(16d));
            view.InformationPanelMargin.Should().Be(16d);
        });
    }

    [Fact]
    public async Task Layout_WhenHostedAsWindowContent_FillsWindowAndAnchorsWindowButtons()
    {
        await DispatchAsync(() =>
        {
            ImageViewerSessionViewModel session = CreateSession(
                false,
                new List<PicaActionDefinition>());
            ImageViewerView view = new(
                session,
                CreateToolMenu(session, false),
                new List<ViewerSettingControl>(),
                ViewerWindowMode.FullScreen,
                CreateEvents());
            Window window = new()
            {
                Width = 1000d,
                Height = 600d,
                Content = view
            };

            try
            {
                window.Show();

                view.Bounds.Size.Should().Be(new Size(1000d, 600d));
                view.Root.Bounds.Size.Should().Be(view.Bounds.Size);
                view.CloseButton.Bounds.Right.Should().Be(1000d);
                view.WindowModeButton.Bounds.Right.Should().Be(936d);
                view.FullscreenSettingsButton.Bounds.Right.Should().Be(872d);
            }
            finally
            {
                window.Close();
                view.Dispose();
            }
        });
    }

    [Fact]
    public async Task Layout_WhenMenusAreHosted_PreservesExistingMenuAppearance()
    {
        await DispatchAsync(() =>
        {
            ImageViewerSessionViewModel session = CreateSession(
                false,
                new List<PicaActionDefinition>());
            ImageViewerView view = new(
                session,
                CreateToolMenu(session, false),
                new List<ViewerSettingControl>(),
                ViewerWindowMode.FullScreen,
                CreateEvents());
            Window window = new()
            {
                Content = view
            };

            try
            {
                window.Show();
                Button contextMenuButton =
                    GetMenuButtons(view.ViewerContextMenu)[0];
                DoubleTransition opacityTransition = view.ViewerContextMenu
                    .Transitions
                    .Should()
                    .ContainSingle()
                    .Which
                    .Should()
                    .BeOfType<DoubleTransition>()
                    .Subject;
                SolidColorBrush menuBackground = view.ViewerContextMenu
                    .Background
                    .Should()
                    .BeOfType<SolidColorBrush>()
                    .Subject;

                view.ViewerContextMenu.Padding.Should().Be(new Thickness(6d));
                view.ViewerContextMenu.CornerRadius.Should().Be(new CornerRadius(8d));
                menuBackground.Color.Should().Be(Color.FromArgb(232, 24, 24, 24));
                opacityTransition.Duration.Should().Be(TimeSpan.FromSeconds(0.16d));
                view.ToolMenu.Padding.Should().Be(view.ViewerContextMenu.Padding);
                view.ToolMenu.CornerRadius.Should().Be(
                    view.ViewerContextMenu.CornerRadius);
                contextMenuButton.MinWidth.Should().Be(148d);
                contextMenuButton.Padding.Should().Be(new Thickness(10d, 8d));
                contextMenuButton.HorizontalContentAlignment
                    .Should()
                    .Be(HorizontalAlignment.Left);
            }
            finally
            {
                window.Close();
                view.Dispose();
            }
        });
    }

    [Fact]
    public async Task Layout_WhenSelectionToolbarIsHosted_PreservesExistingButtonAppearance()
    {
        await DispatchAsync(() =>
        {
            ImageViewerSessionViewModel session = CreateSession(
                false,
                new List<PicaActionDefinition>());
            ImageViewerView view = new(
                session,
                CreateToolMenu(session, false),
                new List<ViewerSettingControl>(),
                ViewerWindowMode.FullScreen,
                CreateEvents());
            Window window = new()
            {
                Content = view
            };

            try
            {
                window.Show();
                Button selectionButton = view.SelectionToolbar
                    .Children
                    .OfType<Button>()
                    .First();
                SolidColorBrush background = selectionButton
                    .Background
                    .Should()
                    .BeOfType<SolidColorBrush>()
                    .Subject;

                selectionButton.Width.Should().Be(42d);
                selectionButton.Height.Should().Be(42d);
                selectionButton.CornerRadius.Should().Be(new CornerRadius(8d));
                background.Color.Should().Be(Color.FromArgb(150, 16, 16, 16));
            }
            finally
            {
                window.Close();
                view.Dispose();
            }
        });
    }

    [Fact]
    public async Task FilteringBinding_WhenFilteringDisabled_HidesCheck()
    {
        await DispatchAsync(async () =>
        {
            ImageViewerSessionViewModel session = CreateSession(
                true,
                new List<PicaActionDefinition>());
            ImageViewerToolMenuViewModel toolMenu =
                CreateToolMenu(session, true);
            using ImageViewerView view = new(
                session,
                toolMenu,
                new List<ViewerSettingControl>(),
                ViewerWindowMode.FullScreen,
                CreateEvents());

            await toolMenu.Settings.ToggleFilteringCommand.ExecuteAsync(
                null);

            GetMenuCheckIcons(view.ToolMenu)[1]
                .IsVisible
                .Should()
                .BeFalse();
            toolMenu.Settings.Dispose();
        });
    }

    [Fact]
    public async Task ToggleCheckerboardBackgroundCommand_WhenEnabled_ShowsMenuCheck()
    {
        await DispatchAsync(async () =>
        {
            ImageViewerSessionViewModel session = CreateSession(
                true,
                new List<PicaActionDefinition>());
            ImageViewerToolMenuViewModel toolMenu =
                CreateToolMenu(session, true);
            using ImageViewerView view = new(
                session,
                toolMenu,
                new List<ViewerSettingControl>(),
                ViewerWindowMode.FullScreen,
                CreateEvents());

            await toolMenu
                .Settings
                .ToggleCheckerboardBackgroundCommand
                .ExecuteAsync(null);

            GetMenuCheckIcons(view.ToolMenu)[0]
                .IsVisible
                .Should()
                .BeTrue();
            toolMenu.Settings.Dispose();
        });
    }

    [Fact]
    public async Task ImageModeBinding_WithChannelsMode_SelectsOnlyChannels()
    {
        await DispatchAsync(() =>
        {
            ImageViewerSessionViewModel session = CreateSession(
                false,
                new List<PicaActionDefinition>());
            using ImageViewerView view = new(
                session,
                CreateToolMenu(session, false),
                new List<ViewerSettingControl>(),
                ViewerWindowMode.FullScreen,
                CreateEvents());

            session.SelectChannelImageModeCommand.Execute(null);

            List<PathIcon> checkIcons = GetMenuCheckIcons(view.ModeMenu);
            checkIcons[0].IsVisible.Should().BeFalse();
            checkIcons[1].IsVisible.Should().BeTrue();
        });
    }

    [Fact]
    public async Task Constructor_WithToolMenu_BindsStateChangesToViewModelCommands()
    {
        await DispatchAsync(() =>
        {
            ImageViewerSessionViewModel session = CreateSession(
                false,
                new List<PicaActionDefinition>());
            ImageViewerToolMenuViewModel toolMenu =
                CreateToolMenu(session, false);
            using ImageViewerView view = new(
                session,
                toolMenu,
                new List<ViewerSettingControl>(),
                ViewerWindowMode.FullScreen,
                CreateEvents());
            List<Button> toolMenuButtons = GetMenuButtons(view.ToolMenu);
            List<Button> modeMenuButtons = GetMenuButtons(view.ModeMenu);

            toolMenuButtons[0].Command.Should().BeSameAs(
                toolMenu.Settings.ToggleCheckerboardBackgroundCommand);
            toolMenuButtons[1].Command.Should().BeSameAs(
                toolMenu.Settings.ToggleFilteringCommand);
            modeMenuButtons[0].Command.Should().BeSameAs(
                session.SelectMainImageModeCommand);
            modeMenuButtons[1].Command.Should().BeSameAs(
                session.SelectChannelImageModeCommand);
        });
    }

    [Fact]
    public async Task Constructor_WithToolMenu_DisplaysTransparencyBackgroundText()
    {
        await DispatchAsync(() =>
        {
            ImageViewerSessionViewModel session = CreateSession(
                false,
                new List<PicaActionDefinition>());
            ImageViewerToolMenuViewModel toolMenu =
                CreateToolMenu(session, false);
            using ImageViewerView view = new(
                session,
                toolMenu,
                new List<ViewerSettingControl>(),
                ViewerWindowMode.FullScreen,
                CreateEvents());

            string? text = GetMenuButtonText(
                GetMenuButtons(view.ToolMenu)[0]);

            text.Should().Be("Прозрачный фон");
        });
    }

    private static void AssertRenderedRegionEqual(
        AvaloniaBitmap expected,
        AvaloniaBitmap actual,
        PixelRect region)
    {
        using MemoryStream expectedStream = new();
        expected.Save(expectedStream);
        expectedStream.Position = 0;
        using SKBitmap expectedPixels =
            SKBitmap.Decode(expectedStream)
            ?? throw new InvalidOperationException(
                "The expected checkerboard frame could not be decoded.");
        using MemoryStream actualStream = new();
        actual.Save(actualStream);
        actualStream.Position = 0;
        using SKBitmap actualPixels =
            SKBitmap.Decode(actualStream)
            ?? throw new InvalidOperationException(
                "The actual checkerboard frame could not be decoded.");
        List<SKColor> expectedRegion = [];
        List<SKColor> actualRegion = [];

        for (int y = region.Y; y < region.Bottom; y++)
        {
            for (int x = region.X; x < region.Right; x++)
            {
                expectedRegion.Add(expectedPixels.GetPixel(x, y));
                actualRegion.Add(actualPixels.GetPixel(x, y));
            }
        }

        expectedRegion.Should().Contain(
            new SKColor(213, 217, 222, 255));
        expectedRegion.Should().Contain(
            new SKColor(180, 186, 194, 255));
        actualRegion.Should().Equal(expectedRegion);
    }

    private static ImageViewerViewEvents CreateEvents()
    {
        return new ImageViewerViewEvents
        {
            ZoomOutClicked = IgnoreRoutedEvent,
            ResetClicked = IgnoreRoutedEvent,
            ZoomInClicked = IgnoreRoutedEvent,
            ToolMenuClicked = IgnoreRoutedEvent,
            CheckerboardBackgroundMenuClicked = IgnoreRoutedEvent,
            FilteringMenuClicked = IgnoreRoutedEvent,
            ModeMenuClicked = IgnoreRoutedEvent,
            MainModeMenuClicked = IgnoreRoutedEvent,
            ChannelModeMenuClicked = IgnoreRoutedEvent,
            CloseClicked = IgnoreRoutedEvent,
            WindowModeClicked = IgnoreRoutedEvent,
            SettingsClicked = IgnoreRoutedEvent,
            ContextCopyClicked = IgnoreRoutedEvent,
            ContextExternalActionClicked = IgnoreRoutedEvent,
            ContextSaveAsClicked = IgnoreRoutedEvent,
            ContextRevealInFolderClicked = IgnoreRoutedEvent,
            ContextOpenWithClicked = IgnoreRoutedEvent,
            ContextSelectAreaClicked = IgnoreRoutedEvent,
            SelectionCopyClicked = IgnoreRoutedEvent,
            SelectionExternalActionClicked = IgnoreRoutedEvent,
            SelectionOpenWithClicked = IgnoreRoutedEvent,
            SelectionSaveAsClicked = IgnoreRoutedEvent,
            SelectionCancelClicked = IgnoreRoutedEvent,
            WindowResizePointerPressed = IgnorePointerPressedEvent,
            WindowResizePointerMoved = IgnorePointerEvent,
            WindowResizePointerReleased = IgnorePointerReleasedEvent
        };
    }

    private static ImageViewerSessionViewModel CreateSession(
        bool isFilteringEnabled,
        IReadOnlyList<PicaActionDefinition> actions)
    {
        Guid itemId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        PicaImageItem item = new(
            itemId,
            "image.png",
            "image.png");
        PicaViewerRequest request = new(
            new PicaImageItem[] { item },
            itemId,
            actions);

        ImageViewerSession session = new(
            request,
            isFilteringEnabled);

        return new ImageViewerSessionViewModel(session);
    }

    private static ImageViewerToolMenuViewModel CreateToolMenu(
        ImageViewerSessionViewModel session,
        bool isFilteringEnabled)
    {
        PicaViewerRequest request = new(
            new List<PicaImageItem>(),
            Guid.Empty);
        ImageViewerSession settingsSession = new(
            request,
            isFilteringEnabled);
        ImageViewerState state = new()
        {
            IsCheckerboardBackgroundEnabled = false,
            IsFilteringEnabled = isFilteringEnabled
        };
        ViewerWindowPlacement placement = new(
            false,
            null,
            null,
            null,
            null);
        ImageViewerSettingsViewModel settings = new(
            new RecordingImageViewerStateService(state),
            settingsSession,
            new RecordingImageLoadingSettings(),
            new ViewerWindowPlacementProvider(placement),
            new RecordingViewModelErrorHandler(),
            state);

        return new ImageViewerToolMenuViewModel(
            session,
            settings);
    }

    private static string? GetMenuButtonText(Button button)
    {
        if (button.Content is not StackPanel content)
        {
            return null;
        }

        return content.Children.OfType<TextBlock>().SingleOrDefault()?.Text;
    }

    private static List<PathIcon> GetMenuCheckIcons(Border menu)
    {
        return GetMenuButtons(menu)
            .Select(button => button.Content)
            .OfType<StackPanel>()
            .Select(content => content.Children
                .OfType<Grid>()
                .Single()
                .Children
                .OfType<PathIcon>()
                .Single())
            .ToList();
    }

    private static List<Button> GetMenuButtons(Border menu)
    {
        StackPanel items = menu
            .Child
            .Should()
            .BeOfType<StackPanel>()
            .Subject;

        return items.Children
            .OfType<Button>()
            .ToList();
    }

    private static async Task DispatchAsync(Action action)
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(
            typeof(ImageViewerViewTests),
            SessionLock,
            action).ConfigureAwait(false);
    }

    private static async Task DispatchAsync(Func<Task> action)
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(
            typeof(ImageViewerViewTests),
            SessionLock,
            action);
    }

    private static void IgnoreRoutedEvent(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
    }

    private static void IgnorePointerPressedEvent(object? sender, PointerPressedEventArgs e)
    {
        _ = sender;
        _ = e;
    }

    private static void IgnorePointerEvent(object? sender, PointerEventArgs e)
    {
        _ = sender;
        _ = e;
    }

    private static void IgnorePointerReleasedEvent(object? sender, PointerReleasedEventArgs e)
    {
        _ = sender;
        _ = e;
    }
}
