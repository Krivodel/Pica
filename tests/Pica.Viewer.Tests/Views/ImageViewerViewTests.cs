using System.Runtime.InteropServices;

using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using FluentAssertions;
using SkiaSharp;
using Xunit;

using Pica.Protocol;
using Pica.Tests.Common;
using Pica.Viewer.Controls;
using Pica.Viewer.Services;
using Pica.Viewer.Tests;
using Pica.Viewer.Tests.TestDoubles;
using Pica.Viewer.ViewModels;
using Pica.Viewer.Views;

using AvaloniaBitmap = Avalonia.Media.Imaging.Bitmap;

namespace Pica.Viewer.Tests.Views;

[Collection(AvaloniaHeadlessCollection.Name)]
public sealed class ImageViewerViewTests
{
    private const double ExpectedContentNavigationFontSize = 52d / 3d;

    private static readonly SemaphoreSlim SessionLock = new(1, 1);

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder
            .Configure<ViewerTestApplication>()
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

            ImageBrush checkerboardBrush = view
                .CheckerboardPattern
                .Background
                .Should()
                .BeOfType<ImageBrush>()
                .Subject;
            RelativeRect expectedDestinationRect = new(
                0d,
                0d,
                10d,
                10d,
                RelativeUnit.Absolute);
            WriteableBitmap checkerboardBitmap =
                checkerboardBrush.Source
                .Should()
                .BeOfType<WriteableBitmap>()
                .Subject;
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
            checkerboardBitmap.PixelSize.Should().Be(
                new PixelSize(
                    ViewerCheckerboardFactory.TileSize,
                    ViewerCheckerboardFactory.TileSize));
            using ILockedFramebuffer framebuffer =
                checkerboardBitmap.Lock();
            int darkPixelOffset =
                ViewerCheckerboardFactory.TileSize
                / 2
                * 4;
            byte[] lightPixel = new byte[4];
            byte[] darkPixel = new byte[4];
            Marshal.Copy(
                framebuffer.Address,
                lightPixel,
                0,
                lightPixel.Length);
            Marshal.Copy(
                IntPtr.Add(
                    framebuffer.Address,
                    darkPixelOffset),
                darkPixel,
                0,
                darkPixel.Length);
            lightPixel.Should().Equal(
                0xDE,
                0xD9,
                0xD5,
                0xFF);
            darkPixel.Should().Equal(
                0xC2,
                0xBA,
                0xB4,
                0xFF);
            view.CheckerboardPattern.RenderTransformOrigin.Should().Be(
                RelativePoint.TopLeft);
            checkerboardTransform.X.Should().Be(expectedPatternOffsetX);
            checkerboardTransform.Y.Should().Be(patternOffsetY);
        });
    }

    [Fact]
    public async Task AnimationLoadingIndicator_WhenBufferingChanges_ShowsCenteredWithoutInputHandling()
    {
        await DispatchAsync(() =>
        {
            ImageViewerSession sessionState =
                CreateSessionState(
                    false,
                    new List<PicaActionDefinition>());
            using ImageViewerSessionViewModel session =
                new(sessionState);
            using ImageViewerView view = new(
                session,
                CreateToolMenu(session, false),
                new List<ViewerSettingControl>(),
                ViewerWindowMode.FullScreen,
                CreateEvents());

            view.AnimationLoadingIndicator.IsVisible
                .Should()
                .BeFalse();

            sessionState.SetAnimationBuffering(true);

            view.AnimationLoadingIndicator.IsVisible
                .Should()
                .BeTrue();
            view.AnimationLoadingIndicator
                .IsHitTestVisible
                .Should()
                .BeFalse();
            view.AnimationLoadingIndicator
                .HorizontalAlignment
                .Should()
                .Be(HorizontalAlignment.Center);
            view.AnimationLoadingIndicator
                .VerticalAlignment
                .Should()
                .Be(VerticalAlignment.Center);
        });
    }

    [Fact]
    public async Task Constructor_DoesNotCreateContentGroupSelector()
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

            Control? selector = view.FindControl<Control>(
                "ContentGroupSelectorControl");

            selector.Should().BeNull();
        });
    }

    [Fact]
    public async Task ContentNavigationPanel_WithMixedContent_ShowsCountsAndNavigatesImages()
    {
        await DispatchAsync(() =>
        {
            ImageViewerSession sessionState = CreateSessionState(
                false,
                new List<PicaActionDefinition>());
            using ImageViewerSessionViewModel session = new(sessionState);
            sessionState.SetContentGroups(
                new List<ImageContentGroupDefinition>
                {
                    new(
                        ImageContentGroupKind.StillImages,
                        2),
                    new(
                        ImageContentGroupKind.Animation,
                        4)
                }.AsReadOnly(),
                0);
            sessionState.SetFramePresentation(
                2,
                ImageFramePresentationModes.ManualNavigation,
                0);
            using ImageViewerView view = new(
                session,
                CreateToolMenu(session, false),
                new List<ViewerSettingControl>(),
                ViewerWindowMode.FullScreen,
                CreateEvents());
            StackPanel imagesOnlyNavigation = GetRequiredControl<StackPanel>(
                view.ContentNavigationPanel,
                "ImagesOnlyNavigationPanel");
            StackPanel animationNavigation = GetRequiredControl<StackPanel>(
                view.ContentNavigationPanel,
                "AnimationNavigationPanel");
            TextBlock selectedContent = GetRequiredControl<TextBlock>(
                view.ContentNavigationPanel,
                "SelectedContentText");
            Border information = GetRequiredControl<Border>(
                view.ContentNavigationPanel,
                "ContentNavigationInformation");
            Grid controls = GetRequiredControl<Grid>(
                view.ContentNavigationPanel,
                "NavigationControlsPanel");
            Button previousFrame = GetRequiredControl<Button>(
                view.ContentNavigationPanel,
                "PreviousFrameButton");
            Button nextFrame = GetRequiredControl<Button>(
                view.ContentNavigationPanel,
                "NextFrameButton");
            Button previousContent = GetRequiredControl<Button>(
                view.ContentNavigationPanel,
                "PreviousContentButton");
            Button nextContent = GetRequiredControl<Button>(
                view.ContentNavigationPanel,
                "NextContentButton");
            Button playAnimation = GetRequiredControl<Button>(
                view.ContentNavigationPanel,
                "PlayAnimationButton");

            if (nextContent.Command is null)
            {
                throw new InvalidOperationException(
                    "The next content button does not have a command.");
            }

            nextContent.Command.Execute(nextContent.CommandParameter);

            view.ContentNavigationPanel.IsVisible.Should().BeTrue();
            imagesOnlyNavigation.IsVisible.Should().BeFalse();
            animationNavigation.IsVisible.Should().BeTrue();
            selectedContent.Text.Should().Be(
                "Изображение 2/2 · Анимаций: 1");
            selectedContent.FontSize.Should().BeApproximately(
                ExpectedContentNavigationFontSize,
                0.000000000000001d);
            selectedContent.TextAlignment.Should().Be(
                TextAlignment.Center);
            information.Parent.Should().BeSameAs(controls.Parent);
            selectedContent.Parent.Should().NotBeSameAs(nextContent.Parent);
            previousContent.Width.Should().Be(44d);
            previousContent.Height.Should().Be(44d);
            nextContent.Width.Should().Be(44d);
            nextContent.Height.Should().Be(44d);
            ToolTip.GetTip(previousContent).Should().BeNull();
            ToolTip.GetTip(nextContent).Should().BeNull();
            playAnimation.Width.Should().Be(44d);
            playAnimation.Height.Should().Be(44d);
            playAnimation.Command.Should().BeNull();
            ToolTip.GetTip(playAnimation).Should().BeNull();
            animationNavigation.Children
                .OfType<Border>()
                .Should()
                .BeEmpty();
            previousFrame.IsEnabled.Should().BeFalse();
            nextFrame.IsEnabled.Should().BeFalse();
            sessionState.SelectedFrameIndex.Should().Be(1);
        });
    }

    [Fact]
    public async Task ContentNavigationPanel_WithSingleAnimation_ShowsAndNavigatesFrames()
    {
        await DispatchAsync(() =>
        {
            ImageViewerSession sessionState = CreateSessionState(
                false,
                new List<PicaActionDefinition>());
            using ImageViewerSessionViewModel session = new(sessionState);
            sessionState.SetContentGroups(
                new List<ImageContentGroupDefinition>
                {
                    new(
                        ImageContentGroupKind.Animation,
                        4)
                }.AsReadOnly(),
                0);
            sessionState.SetFramePresentation(
                4,
                ImageFramePresentationModes.AutomaticPlayback,
                0);
            using ImageViewerView view = new(
                session,
                CreateToolMenu(session, false),
                new List<ViewerSettingControl>(),
                ViewerWindowMode.FullScreen,
                CreateEvents());
            StackPanel imagesOnlyNavigation = GetRequiredControl<StackPanel>(
                view.ContentNavigationPanel,
                "ImagesOnlyNavigationPanel");
            StackPanel animationNavigation = GetRequiredControl<StackPanel>(
                view.ContentNavigationPanel,
                "AnimationNavigationPanel");
            TextBlock selectedFrame = GetRequiredControl<TextBlock>(
                view.ContentNavigationPanel,
                "SelectedFrameText");
            Button previousFrame = GetRequiredControl<Button>(
                view.ContentNavigationPanel,
                "PreviousFrameButton");
            Button nextFrame = GetRequiredControl<Button>(
                view.ContentNavigationPanel,
                "NextFrameButton");
            Button playAnimation = GetRequiredControl<Button>(
                view.ContentNavigationPanel,
                "PlayAnimationButton");
            Button previousContent = GetRequiredControl<Button>(
                view.ContentNavigationPanel,
                "PreviousContentButton");
            Button nextContent = GetRequiredControl<Button>(
                view.ContentNavigationPanel,
                "NextContentButton");

            if (nextFrame.Command is null)
            {
                throw new InvalidOperationException(
                    "The next frame button does not have a command.");
            }

            nextFrame.Command.Execute(nextFrame.CommandParameter);

            view.ContentNavigationPanel.IsVisible.Should().BeTrue();
            imagesOnlyNavigation.IsVisible.Should().BeFalse();
            animationNavigation.IsVisible.Should().BeTrue();
            previousFrame.IsEnabled.Should().BeTrue();
            nextFrame.IsEnabled.Should().BeTrue();
            previousContent.IsEnabled.Should().BeFalse();
            nextContent.IsEnabled.Should().BeFalse();
            selectedFrame.Text.Should().Be("Кадр 2/4");
            selectedFrame.FontSize.Should().BeApproximately(
                ExpectedContentNavigationFontSize,
                0.000000000000001d);
            selectedFrame.TextAlignment.Should().Be(
                TextAlignment.Center);
            selectedFrame.Parent.Should().NotBeSameAs(nextFrame.Parent);
            previousFrame.Width.Should().Be(44d);
            previousFrame.Height.Should().Be(44d);
            nextFrame.Width.Should().Be(44d);
            nextFrame.Height.Should().Be(44d);
            ToolTip.GetTip(previousFrame).Should().BeNull();
            ToolTip.GetTip(nextFrame).Should().BeNull();
            playAnimation.Width.Should().Be(44d);
            playAnimation.Height.Should().Be(44d);
            playAnimation.Command.Should().BeNull();
            ToolTip.GetTip(playAnimation).Should().BeNull();
            sessionState.SelectedFrameIndex.Should().Be(1);
        });
    }

    [Fact]
    public async Task ContentNavigationPanel_WithMultipleStillImages_ShowsOnlyContentButtons()
    {
        await DispatchAsync(() =>
        {
            ImageViewerSession sessionState = CreateSessionState(
                false,
                new List<PicaActionDefinition>());
            using ImageViewerSessionViewModel session = new(sessionState);
            sessionState.SetContentGroups(
                new List<ImageContentGroupDefinition>
                {
                    new(
                        ImageContentGroupKind.StillImages,
                        3)
                }.AsReadOnly(),
                0);
            sessionState.SetFramePresentation(
                3,
                ImageFramePresentationModes.ManualNavigation,
                0);
            using ImageViewerView view = new(
                session,
                CreateToolMenu(session, false),
                new List<ViewerSettingControl>(),
                ViewerWindowMode.FullScreen,
                CreateEvents());
            StackPanel imagesOnlyNavigation = GetRequiredControl<StackPanel>(
                view.ContentNavigationPanel,
                "ImagesOnlyNavigationPanel");
            StackPanel animationNavigation = GetRequiredControl<StackPanel>(
                view.ContentNavigationPanel,
                "AnimationNavigationPanel");
            Button previousContent = GetRequiredControl<Button>(
                view.ContentNavigationPanel,
                "ImagesOnlyPreviousContentButton");
            Button nextContent = GetRequiredControl<Button>(
                view.ContentNavigationPanel,
                "ImagesOnlyNextContentButton");

            if (nextContent.Command is null)
            {
                throw new InvalidOperationException(
                    "The next content button does not have a command.");
            }

            nextContent.Command.Execute(nextContent.CommandParameter);

            view.ContentNavigationPanel.IsVisible.Should().BeTrue();
            imagesOnlyNavigation.IsVisible.Should().BeTrue();
            animationNavigation.IsVisible.Should().BeFalse();
            previousContent.Width.Should().Be(44d);
            previousContent.Height.Should().Be(44d);
            nextContent.Width.Should().Be(44d);
            nextContent.Height.Should().Be(44d);
            ToolTip.GetTip(previousContent).Should().BeNull();
            ToolTip.GetTip(nextContent).Should().BeNull();
            sessionState.SelectedFrameIndex.Should().Be(1);
        });
    }

    [Fact]
    public async Task ContentNavigationPanel_WhenSelectedTypeChanges_KeepsControlRowWidth()
    {
        await DispatchAsync(() =>
        {
            ImageViewerSession sessionState = CreateSessionState(
                false,
                new List<PicaActionDefinition>());
            using ImageViewerSessionViewModel session = new(sessionState);
            sessionState.SetContentGroups(
                new List<ImageContentGroupDefinition>
                {
                    new(
                        ImageContentGroupKind.StillImages,
                        2),
                    new(
                        ImageContentGroupKind.Animation,
                        4)
                }.AsReadOnly(),
                0);
            sessionState.SetFramePresentation(
                2,
                ImageFramePresentationModes.ManualNavigation,
                0);
            using ImageViewerView view = new(
                session,
                CreateToolMenu(session, false),
                new List<ViewerSettingControl>(),
                ViewerWindowMode.FullScreen,
                CreateEvents());
            Grid controls = GetRequiredControl<Grid>(
                view.ContentNavigationPanel,
                "NavigationControlsPanel");
            Size availableSize = new(
                double.PositiveInfinity,
                double.PositiveInfinity);
            controls.Measure(availableSize);
            double stillImageControlWidth = controls.DesiredSize.Width;

            sessionState.SelectContentGroup(1);
            sessionState.SetFramePresentation(
                4,
                ImageFramePresentationModes.AutomaticPlayback,
                0);
            controls.Measure(availableSize);

            controls.DesiredSize.Width.Should().Be(stillImageControlWidth);
        });
    }

    [Fact]
    public async Task ContentNavigationPanel_WhenAnimationFrameNumberChanges_KeepsInformationWidth()
    {
        await DispatchAsync(() =>
        {
            const int FrameCount = 120;
            ImageViewerSession sessionState = CreateSessionState(
                false,
                new List<PicaActionDefinition>());
            using ImageViewerSessionViewModel session = new(sessionState);
            sessionState.SetContentGroups(
                new List<ImageContentGroupDefinition>
                {
                    new(
                        ImageContentGroupKind.StillImages,
                        2),
                    new(
                        ImageContentGroupKind.Animation,
                        FrameCount),
                    new(
                        ImageContentGroupKind.Animation,
                        FrameCount),
                    new(
                        ImageContentGroupKind.Animation,
                        FrameCount)
                }.AsReadOnly(),
                1);
            sessionState.SetFramePresentation(
                FrameCount,
                ImageFramePresentationModes.AutomaticPlayback,
                0);
            using ImageViewerView view = new(
                session,
                CreateToolMenu(session, false),
                new List<ViewerSettingControl>(),
                ViewerWindowMode.FullScreen,
                CreateEvents());
            Border information = GetRequiredControl<Border>(
                view.ContentNavigationPanel,
                "ContentNavigationInformation");
            Size availableSize = new(
                double.PositiveInfinity,
                double.PositiveInfinity);
            information.Measure(availableSize);
            double firstFrameWidth = information.DesiredSize.Width;

            sessionState.SetFramePresentation(
                FrameCount,
                ImageFramePresentationModes.AutomaticPlayback,
                57);
            information.Measure(availableSize);
            double middleFrameWidth = information.DesiredSize.Width;
            sessionState.SetFramePresentation(
                FrameCount,
                ImageFramePresentationModes.AutomaticPlayback,
                FrameCount - 1);
            information.Measure(availableSize);

            middleFrameWidth.Should().Be(firstFrameWidth);
            information.DesiredSize.Width.Should().Be(firstFrameWidth);
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
            view.ContentNavigationPanel.Margin.Should().Be(
                new Thickness(0d, 0d, 0d, 78d));
            view.ContentNavigationPanel.HorizontalAlignment
                .Should()
                .Be(HorizontalAlignment.Center);
            view.ContentNavigationPanel.VerticalAlignment
                .Should()
                .Be(VerticalAlignment.Bottom);
            view.ContentNavigationPanel.Opacity.Should().Be(0d);
            view.ContentNavigationPanel.IsHitTestVisible.Should().BeFalse();
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
            viewerChrome.Children.IndexOf(view.ContentNavigationPanel).Should().Be(3);
            viewerChrome.Children.IndexOf(view.ImageInformationPanel).Should().Be(4);
            viewerChrome.Children.IndexOf(view.FullscreenSettingsButton).Should().Be(5);
            viewerChrome.Children.IndexOf(view.WindowModeButton).Should().Be(6);
            viewerChrome.Children.IndexOf(view.CloseButton).Should().Be(7);
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
    public async Task Layout_WhenSubmenuButtonsAreHosted_AlignsIndicatorsAtRight()
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
                view.ViewerContextMenu.IsVisible = true;
                view.ToolMenu.IsVisible = true;
                window.Show();
                PathIcon contextMenuIndicator = GetSubmenuIndicator(
                    view.ContextOpenWithButton);
                PathIcon toolMenuIndicator = GetSubmenuIndicator(
                    view.ModeMenuButton);
                Thickness expectedMargin = new(10d, 0d, 2d, 0d);

                view.ContextOpenWithButton.HorizontalContentAlignment
                    .Should()
                    .Be(HorizontalAlignment.Stretch);
                view.ModeMenuButton.HorizontalContentAlignment
                    .Should()
                    .Be(HorizontalAlignment.Stretch);
                contextMenuIndicator.Margin.Should().Be(expectedMargin);
                toolMenuIndicator.Margin.Should().Be(expectedMargin);
                contextMenuIndicator.VerticalAlignment
                    .Should()
                    .Be(VerticalAlignment.Center);
                toolMenuIndicator.VerticalAlignment
                    .Should()
                    .Be(VerticalAlignment.Center);
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
        return new ImageViewerSessionViewModel(
            CreateSessionState(
                isFilteringEnabled,
                actions));
    }

    private static ImageViewerSession CreateSessionState(
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

        return new ImageViewerSession(
            request,
            isFilteringEnabled);
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

    private static PathIcon GetSubmenuIndicator(Button button)
    {
        Grid content = button
            .Content
            .Should()
            .BeOfType<Grid>()
            .Subject;

        return content.Children
            .OfType<PathIcon>()
            .Single();
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

    private static TControl GetRequiredControl<TControl>(
        Control parent,
        string name)
        where TControl : Control
    {
        return parent.FindControl<TControl>(name)
            ?? throw new InvalidOperationException(
                $"The control '{name}' is unavailable.");
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
