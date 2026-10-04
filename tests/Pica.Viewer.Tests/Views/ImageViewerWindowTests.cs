using System.Runtime.CompilerServices;

using Microsoft.Extensions.Logging.Abstractions;

using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using SkiaSharp;
using Xunit;

using Pica.Protocol;
using Pica.Viewer.Controls;
using Pica.Tests.Common;
using Pica.Viewer.Resources;
using Pica.Viewer.Services;
using Pica.Viewer.Tests;
using Pica.Viewer.Tests.TestDoubles;
using Pica.Viewer.ViewModels;
using Pica.Viewer.Views;

namespace Pica.Viewer.Tests.Views;

[Collection(AvaloniaHeadlessCollection.Name)]
public sealed class ImageViewerWindowTests
{
    private const int SourceImageWidth = 640;
    private const int SourceImageHeight = 480;
    private const int TestTimeoutSeconds = 10;
    private const int ClipboardNavigationTestTimeoutSeconds = 30;

    private static readonly SemaphoreSlim SessionLock = new(1, 1);
    private static readonly Guid ItemId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder
            .Configure<ViewerTestApplication>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }

    [Fact]
    public async Task Show_WithEmptySession_DisplaysCenteredMessageWithoutInterceptingInput()
    {
        await DispatchAsync(() =>
        {
            ImageViewerWindow window = CreateWindow(CreateEmptyRequest(), CreateWindowedState(),
                new RecordingImageChannelBitmapLoader());
            ImageViewerView view = window.Content as ImageViewerView
                ?? throw new InvalidOperationException("The viewer content must be created.");

            try
            {
                window.Show();
                window.UpdateLayout();

                TextBlock message = view.GetVisualDescendants().OfType<TextBlock>()
                    .Single(text => text.Text == ViewerUiStrings.NoImages);
                message.IsVisible.Should().BeTrue();
                message.IsHitTestVisible.Should().BeFalse();
                Point messageCenter = message.TranslatePoint(
                    new Point(message.Bounds.Width / 2d, message.Bounds.Height / 2d), view.ViewerArea)
                    ?? throw new InvalidOperationException("The empty message must be attached to the viewer.");
                messageCenter.X.Should().BeApproximately(view.ViewerArea.Bounds.Width / 2d, 0.5d);
                messageCenter.Y.Should().BeApproximately(view.ViewerArea.Bounds.Height / 2d, 0.5d);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task PasteFromClipboardAsync_WithEmptySession_HidesEmptyMessageAndKeepsImageOnNavigation()
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(typeof(SkiaViewerTestSession), SessionLock, async () =>
        {
            DelegateClipboardImageReader reader = new()
            {
                Read = _ => Task.FromResult<IReadOnlyList<ClipboardImageInput>>(
                    new ClipboardImageInput[] { ClipboardImageInput.FromBitmap(CreateBitmap(SourceImageWidth, SourceImageHeight)) })
            };
            ImageViewerWindow window = CreateWindow(CreateEmptyRequest(), CreateWindowedState(),
                new RecordingImageChannelBitmapLoader(), clipboardReader: reader);
            ImageViewerView view = window.Content as ImageViewerView
                ?? throw new InvalidOperationException("The viewer content must be created.");
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(TestTimeoutSeconds));

            try
            {
                window.Show();
                TextBlock message = view.GetVisualDescendants().OfType<TextBlock>()
                    .Single(text => text.Text == ViewerUiStrings.NoImages);
                message.IsVisible.Should().BeTrue();

                await window.PasteFromClipboardAsync(timeout.Token);
                await WaitForImageSourceAsync(view,
                    source => source.PixelSize == new PixelSize(SourceImageWidth, SourceImageHeight), timeout.Token);
                window.KeyPress(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, null);

                message.IsVisible.Should().BeFalse();
                view.Image.Source.Should().NotBeNull();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task Show_WithImageFile_HidesEmptyMessage()
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(typeof(SkiaViewerTestSession), SessionLock, async () =>
        {
            using PicaTemporaryDirectory directory = new();
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(TestTimeoutSeconds));
            ImageViewerState state = CreateWindowedState();
            state.IsFastLoadingEnabled = false;
            ImageViewerWindow window = await CreateLoadedWindowAsync(directory.DirectoryPath, state, timeout.Token);

            try
            {
                TextBlock message = window.GetVisualDescendants().OfType<TextBlock>()
                    .Single(text => text.Text == ViewerUiStrings.NoImages);

                message.IsVisible.Should().BeFalse();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task ShowError_InViewerSettingsWithApplicationTheme_DisplaysRedText()
    {
        await DispatchAsync(() =>
        {
            ImageViewerWindow window = CreateWindow(CreateEmptyRequest(), CreateWindowedState(),
                new RecordingImageChannelBitmapLoader());
            ViewerSettingErrorControl error = new(_ => "Это сочетание уже занято.", NullLogger.Instance);
            error.Classes.Add("viewer-menu-text");
            Border panel = new() { Child = error };
            panel.Classes.Add("modal-glass-panel");
            ImageViewerView view = window.Content as ImageViewerView
                ?? throw new InvalidOperationException("The viewer content must be created.");
            Grid layer = view.FindControl<Grid>("ViewerDynamicLayerControl")
                ?? throw new InvalidOperationException("The viewer layer must be created.");
            layer.Children.Add(panel);

            try
            {
                window.Show();

                error.ShowError(new IOException("Private error"));

                SolidColorBrush foreground = error.Foreground.Should().BeOfType<SolidColorBrush>().Subject;
                foreground.Color.Should().Be(Color.Parse("#FFE5484D"));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task Constructor_WithCheckerboardBackgroundEnabled_ShowsLayer()
    {
        await DispatchAsync(() =>
        {
            ImageViewerState state = new()
            {
                IsCheckerboardBackgroundEnabled = true
            };
            ImageViewerWindow window = CreateWindow(
                CreateEmptyRequest(),
                state,
                new RecordingImageChannelBitmapLoader());

            try
            {
                window.Show();
                ImageViewerView view = window.Content as ImageViewerView
                    ?? throw new InvalidOperationException(
                        "The viewer content must be created.");

                view.CheckerboardBackground.IsVisible.Should().BeTrue();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task TitleBarAutoHide_WithPointerEnteringAndLeavingTitleBar_KeepsViewportAndImagePlacement()
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(typeof(SkiaViewerTestSession), SessionLock, async () =>
        {
            using PicaTemporaryDirectory directory = new();
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(TestTimeoutSeconds));
            ImageViewerState state = CreateWindowedState();
            state.IsFastLoadingEnabled = false;
            state.ResizeBehavior = WindowResizeBehavior.Free;
            ImageViewerWindow window = await CreateLoadedWindowAsync(directory.DirectoryPath, state, timeout.Token);
            ImageViewerView view = window.Content as ImageViewerView
                ?? throw new InvalidOperationException("The viewer content must be created.");

            try
            {
                await Task.Delay(250, timeout.Token);
                window.UpdateLayout();
                Control titleBar = window.GetVisualDescendants().OfType<Control>()
                    .Single(control => control.Name == "PART_TitleBar");
                Rect viewportBounds = view.ViewerArea.Bounds;
                Size clientSize = window.ClientSize;
                view.Image.Width *= 2d;
                view.Image.Height *= 2d;
                Canvas.SetLeft(view.Image, 90d);
                Canvas.SetTop(view.Image, 70d);
                Rect imagePlacement = new(Canvas.GetLeft(view.Image), Canvas.GetTop(view.Image),
                    view.Image.Width, view.Image.Height);
                Point titleBarCenter = titleBar.TranslatePoint(
                    new Point(titleBar.Bounds.Width / 2d, titleBar.Bounds.Height / 2d), window)
                    ?? throw new InvalidOperationException("The title bar must be attached to the window.");
                Point imageCenter = new(clientSize.Width / 2d, clientSize.Height / 2d);

                titleBar.Opacity.Should().Be(0d);
                titleBar.IsHitTestVisible.Should().BeFalse();
                viewportBounds.Size.Should().Be(clientSize);

                foreach (Point position in new Point[] { titleBarCenter, imageCenter, titleBarCenter, imageCenter })
                {
                    window.MouseMove(position, RawInputModifiers.None);
                    window.UpdateLayout();

                    titleBar.IsHitTestVisible.Should().Be(position == titleBarCenter);
                    await WaitForOpacityAsync(titleBar, position == titleBarCenter ? 1d : 0d, timeout.Token);
                    view.ViewerArea.Bounds.Should().Be(viewportBounds);
                    window.ClientSize.Should().Be(clientSize);
                    new Rect(Canvas.GetLeft(view.Image), Canvas.GetTop(view.Image),
                        view.Image.Width, view.Image.Height).Should().Be(imagePlacement);
                }

                window.MouseMove(titleBarCenter, RawInputModifiers.None);
                window.MouseMove(new Point(-1d, -1d), RawInputModifiers.None);
                titleBar.IsHitTestVisible.Should().BeFalse();
                await WaitForOpacityAsync(titleBar, 0d, timeout.Token);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task TitleBarAutoHide_WithBriefPointerPassAndReentry_FadesWithoutFlashingOrRestartingOnMovement()
    {
        await DispatchAsync(async () =>
        {
            ImageViewerWindow window = CreateWindow(CreateEmptyRequest(), CreateWindowedState(),
                new RecordingImageChannelBitmapLoader());
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(TestTimeoutSeconds));

            try
            {
                window.Show();
                window.UpdateLayout();
                Control titleBar = window.GetVisualDescendants().OfType<Control>()
                    .Single(control => control.Name == "PART_TitleBar");
                Point titleBarCenter = new(window.ClientSize.Width / 2d, titleBar.Bounds.Height / 2d);
                Point imageCenter = new(window.ClientSize.Width / 2d, window.ClientSize.Height / 2d);

                window.MouseMove(titleBarCenter, RawInputModifiers.None);

                titleBar.Opacity.Should().BeApproximately(0d, 0.001d);
                titleBar.IsHitTestVisible.Should().BeTrue();
                await Task.Delay(50, timeout.Token);
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                titleBar.Opacity.Should().BeInRange(0d, 0.02d);
                double initialOpacity = titleBar.Opacity;

                window.MouseMove(imageCenter, RawInputModifiers.None);

                titleBar.Opacity.Should().BeApproximately(initialOpacity, 0.001d);
                titleBar.IsHitTestVisible.Should().BeFalse();
                await WaitForOpacityAsync(titleBar, 0d, timeout.Token);
                await Task.Delay(450, timeout.Token);
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                titleBar.Opacity.Should().Be(0d);

                window.MouseMove(titleBarCenter, RawInputModifiers.None);
                await Task.Delay(125, timeout.Token);
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                double partialOpacity = titleBar.Opacity;
                partialOpacity.Should().BeInRange(0.01d, 0.9d);
                window.MouseMove(imageCenter, RawInputModifiers.None);
                titleBar.Opacity.Should().BeInRange(0.01d, 0.9d);
                window.MouseMove(titleBarCenter, RawInputModifiers.None);
                titleBar.Opacity.Should().BeInRange(0.01d, 0.9d);

                for (int moveIndex = 0; moveIndex < 6; moveIndex++)
                {
                    await Task.Delay(50, timeout.Token);
                    window.MouseMove(titleBarCenter + new Vector(moveIndex, 0d), RawInputModifiers.None);
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                }

                titleBar.Opacity.Should().Be(1d);
                titleBar.IsHitTestVisible.Should().BeTrue();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TitleBarAutoHide_DuringNativeWindowDrag_RespectsSettingAndRestoresHover(bool autoHide)
    {
        await DispatchAsync(async () =>
        {
            ImageViewerState state = CreateWindowedState();
            state.AutoHideWindowTitleBar = autoHide;
            ImageViewerWindow window = CreateWindow(CreateEmptyRequest(), state, new RecordingImageChannelBitmapLoader());
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(TestTimeoutSeconds));

            try
            {
                window.Show();
                window.UpdateLayout();
                Control titleBar = window.GetVisualDescendants().OfType<Control>()
                    .Single(control => control.Name == "PART_TitleBar");
                Point titleBarCenter = new(window.ClientSize.Width / 2d, titleBar.Bounds.Height / 2d);
                Size clientSize = window.ClientSize;
                window.MouseMove(titleBarCenter, RawInputModifiers.None);
                await WaitForOpacityAsync(titleBar, 1d, timeout.Token);

                window.SetNativeWindowDragActive(true);

                titleBar.Opacity.Should().Be(1d);
                titleBar.IsHitTestVisible.Should().Be(!autoHide);

                window.MouseMove(titleBarCenter, RawInputModifiers.LeftMouseButton);
                await WaitForOpacityAsync(titleBar, autoHide ? 0d : 1d, timeout.Token);
                window.MouseMove(titleBarCenter, RawInputModifiers.LeftMouseButton);

                titleBar.Opacity.Should().Be(autoHide ? 0d : 1d);
                IControlTemplate template = window.Template
                    ?? throw new InvalidOperationException("The window template must be applied.");
                window.Template = null;
                window.ApplyTemplate();
                window.Template = template;
                window.ApplyTemplate();
                window.UpdateLayout();
                titleBar = window.GetVisualDescendants().OfType<Control>()
                    .Single(control => control.Name == "PART_TitleBar");
                await Task.Delay(300, timeout.Token);
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                titleBar.Opacity.Should().Be(autoHide ? 0d : 1d);
                window.ClientSize.Should().Be(clientSize);

                window.SetNativeWindowDragActive(false);

                await WaitForOpacityAsync(titleBar, 1d, timeout.Token);
                titleBar.IsHitTestVisible.Should().BeTrue();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public async Task TitleBarAutoHide_DuringResizeAndAfterReleaseOrCaptureLoss_RespectsSettingAndRestoresHover(
        bool autoHide,
        bool loseCapture)
    {
        await DispatchAsync(async () =>
        {
            using PicaTemporaryDirectory directory = new();
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(TestTimeoutSeconds));
            ImageViewerState state = CreateWindowedState();
            state.AutoHideWindowTitleBar = autoHide;
            state.IsFastLoadingEnabled = false;
            state.ResizeBehavior = WindowResizeBehavior.Free;
            ImageViewerWindow window = await CreateLoadedWindowAsync(directory.DirectoryPath, state, timeout.Token);
            ImageViewerView view = window.Content as ImageViewerView
                ?? throw new InvalidOperationException("The viewer content must be created.");
            IPointer? resizePointer = null;

            try
            {
                Control titleBar = window.GetVisualDescendants().OfType<Control>()
                    .Single(control => control.Name == "PART_TitleBar");
                Point titleBarCenter = new(window.ClientSize.Width / 2d, titleBar.Bounds.Height / 2d);
                window.MouseMove(titleBarCenter, RawInputModifiers.None);
                await WaitForOpacityAsync(titleBar, 1d, timeout.Token);
                Border resizeGrip = view.WindowResizeOverlay.Children.OfType<Border>()
                    .Single(border => border.Tag is WindowSizingEdges.Top);
                resizeGrip.AddHandler(InputElement.PointerPressedEvent,
                    (_, e) => resizePointer = e.Pointer, RoutingStrategies.Bubble, handledEventsToo: true);
                Point resizeStart = new(window.ClientSize.Width / 2d, 1d);

                window.MouseDown(resizeStart, MouseButton.Left, RawInputModifiers.None);

                titleBar.Opacity.Should().BeGreaterThan(0d);
                titleBar.IsHitTestVisible.Should().Be(!autoHide);

                window.MouseMove(titleBarCenter, RawInputModifiers.LeftMouseButton);
                await Task.Delay(300, timeout.Token);
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                titleBar.Opacity.Should().Be(autoHide ? 0d : 1d);
                titleBar.IsHitTestVisible.Should().Be(!autoHide);
                IPointer pointer = resizePointer
                    ?? throw new InvalidOperationException("The resize grip must receive the mouse press.");

                if (loseCapture)
                {
                    pointer.Capture(view.ViewerArea);
                    pointer.Captured.Should().BeSameAs(view.ViewerArea);
                }
                else
                {
                    window.MouseUp(titleBarCenter, MouseButton.Left, RawInputModifiers.None);
                    pointer.Captured.Should().BeNull();
                }

                double completedHeight = window.Height;
                window.MouseMove(new Point(titleBarCenter.X, window.ClientSize.Height / 2d), RawInputModifiers.None);
                window.Height.Should().Be(completedHeight);
                pointer.Capture(null);
                window.MouseMove(titleBarCenter, RawInputModifiers.None);
                await WaitForOpacityAsync(titleBar, 1d, timeout.Token);
                titleBar.IsHitTestVisible.Should().BeTrue();
            }
            finally
            {
                resizePointer?.Capture(null);
                window.Close();
            }
        });
    }

    [Theory]
    [InlineData(WindowResizeBehavior.AlwaysFitImage)]
    [InlineData(WindowResizeBehavior.FitWhenWindowed)]
    public async Task TitleBarAutoHide_WhenSettingChangesInFittedWindow_RefitsImageWithoutBlackBars(
        WindowResizeBehavior resizeBehavior)
    {
        await DispatchAsync(async () =>
        {
            using PicaTemporaryDirectory directory = new();
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(TestTimeoutSeconds));
            ImageViewerState state = CreateWindowedState();
            state.AutoHideWindowTitleBar = false;
            state.ResizeBehavior = resizeBehavior;
            state.IsFastLoadingEnabled = false;
            ImageViewerWindow window = await CreateLoadedWindowAsync(directory.DirectoryPath, state, timeout.Token);
            ImageViewerView view = window.Content as ImageViewerView
                ?? throw new InvalidOperationException("The viewer content must be created.");

            try
            {
                await Task.Delay(250, timeout.Token);
                window.UpdateLayout();
                CheckBox setting = GetTitleBarAutoHideSetting(view, out ViewerSettingsContentControl settingsContent);

                foreach (bool autoHide in new bool[] { true, false, true })
                {
                    setting.IsChecked = autoHide;
                    await settingsContent.Completion;
                    window.UpdateLayout();
                    await Task.Delay(250, timeout.Token);
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                    window.UpdateLayout();

                    view.ViewerArea.Bounds.Width.Should().BeApproximately(view.Image.Width, 0.01d);
                    view.ViewerArea.Bounds.Height.Should().BeApproximately(view.Image.Height, 0.01d);
                    Canvas.GetLeft(view.Image).Should().BeApproximately(0d, 0.01d);
                    Canvas.GetTop(view.Image).Should().BeApproximately(0d, 0.01d);
                }
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task TitleBarAutoHide_WhenSettingChangesAndWindowModeChanges_UpdatesChrome()
    {
        await DispatchAsync(async () =>
        {
            ImageViewerWindow window = CreateWindow(CreateEmptyRequest(), CreateWindowedState(),
                new RecordingImageChannelBitmapLoader());
            ImageViewerView view = window.Content as ImageViewerView
                ?? throw new InvalidOperationException("The viewer content must be created.");
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(TestTimeoutSeconds));

            try
            {
                window.Show();
                window.UpdateLayout();
                Control titleBar = window.GetVisualDescendants().OfType<Control>()
                    .Single(control => control.Name == "PART_TitleBar");
                Panel overlayParent = titleBar.Parent.Should().BeAssignableTo<Panel>().Subject;
                CheckBox setting = GetTitleBarAutoHideSetting(view, out ViewerSettingsContentControl settingsContent);
                Size clientSize = window.ClientSize;

                setting.IsChecked = false;
                await settingsContent.Completion;
                window.UpdateLayout();

                titleBar.Opacity.Should().Be(1d);
                titleBar.IsHitTestVisible.Should().BeTrue();
                titleBar.Parent.Should().NotBeSameAs(overlayParent);
                view.ViewerArea.Bounds.Height.Should().BeLessThan(clientSize.Height);

                setting.IsChecked = true;
                await settingsContent.Completion;
                window.UpdateLayout();

                titleBar.Parent.Should().BeSameAs(overlayParent);
                titleBar.Opacity.Should().Be(0d);
                view.ViewerArea.Bounds.Size.Should().Be(clientSize);

                window.MouseMove(new Point(clientSize.Width / 2d, titleBar.Bounds.Height / 2d), RawInputModifiers.None);
                titleBar.Opacity.Should().BeApproximately(0d, 0.001d);
                titleBar.IsHitTestVisible.Should().BeTrue();
                window.WindowState = WindowState.FullScreen;
                window.UpdateLayout();
                titleBar.IsVisible.Should().BeFalse();
                await Task.Delay(450, timeout.Token);
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                titleBar.Opacity.Should().Be(0d);
                window.WindowState = WindowState.Normal;
                window.UpdateLayout();
                window.MouseMove(new Point(window.ClientSize.Width / 2d, window.ClientSize.Height / 2d), RawInputModifiers.None);
                titleBar.IsVisible.Should().BeTrue();
                titleBar.IsHitTestVisible.Should().BeFalse();
                await WaitForOpacityAsync(titleBar, 0d, timeout.Token);
                view.ViewerArea.Bounds.Size.Should().Be(window.ClientSize);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task TitleBarDoubleClick_InWindowedMode_EntersFullScreenMode()
    {
        await DispatchAsync(() =>
        {
            ImageViewerWindow window = CreateWindow(
                CreateEmptyRequest(),
                CreateWindowedState(),
                new RecordingImageChannelBitmapLoader());

            try
            {
                window.Show();
                window.CanResize.Should().BeFalse();
                window.CanMaximize.Should().BeFalse();
                Button maximizeButton = window
                    .GetVisualDescendants()
                    .OfType<Button>()
                    .Single(button => string.Equals(
                        button.Name,
                        "PART_MaximizeButton",
                        StringComparison.Ordinal));
                Control titleBar = window
                    .GetVisualDescendants()
                    .OfType<Control>()
                    .Single(control => string.Equals(
                        control.Name,
                        "PART_TitleBar",
                        StringComparison.Ordinal));
                maximizeButton.IsVisible.Should().BeFalse();
                titleBar
                    .GetVisualDescendants()
                    .Prepend(titleBar)
                    .Should()
                    .NotContain(visual =>
                        WindowDecorationProperties.GetElementRole(visual)
                        == WindowDecorationsElementRole.TitleBar);
                Point titleBarCenter = titleBar.TranslatePoint(
                    new Point(
                        titleBar.Bounds.Width / 2d,
                        titleBar.Bounds.Height / 2d),
                    window)
                    ?? throw new InvalidOperationException(
                        "The title bar is not attached to the test window.");

                DoubleClick(window, titleBarCenter);

                window.CurrentWindowMode
                    .Should()
                    .Be(ViewerWindowMode.FullScreen);
                window.WindowState.Should().Be(WindowState.FullScreen);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ViewerDoubleClick_OnBackgroundOrImage_TogglesWindowMode(
        bool clickBackground)
    {
        await DispatchAsync(async () =>
        {
            using PicaTemporaryDirectory directory = new();
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(TestTimeoutSeconds));
            ImageViewerState state = CreateBackgroundClickState();
            ImageViewerWindow window = await CreateLoadedWindowAsync(directory.DirectoryPath, state, timeout.Token);

            try
            {
                Point position = GetViewerClickPosition(window, clickBackground);

                DoubleClick(window, position);

                window.CurrentWindowMode.Should().Be(ViewerWindowMode.FullScreen);

                window.UpdateLayout();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                position = GetViewerClickPosition(window, clickBackground);

                DoubleClick(window, position);

                window.CurrentWindowMode.Should().Be(ViewerWindowMode.Windowed);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task BackgroundDoubleClick_WhenDisabledOrSaving_PreservesWindowMode(
        bool expandOnDoubleClick,
        bool isSaving)
    {
        await DispatchAsync(async () =>
        {
            using PicaTemporaryDirectory directory = new();
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(TestTimeoutSeconds));
            ImageViewerState state = CreateBackgroundClickState();
            state.ExpandOnDoubleClick = expandOnDoubleClick;
            ImageViewerWindow window = await CreateLoadedWindowAsync(directory.DirectoryPath, state, timeout.Token);

            try
            {
                Point position = GetViewerClickPosition(window, true);
                window.SetSavingInteractionState(isSaving);

                DoubleClick(window, position);

                window.CurrentWindowMode.Should().Be(ViewerWindowMode.Windowed);
            }
            finally
            {
                window.SetSavingInteractionState(false);
                window.Close();
            }
        });
    }

    [Theory]
    [InlineData(MouseButton.Left, RawInputModifiers.Control)]
    [InlineData(MouseButton.Middle, RawInputModifiers.None)]
    public async Task BackgroundDoubleClick_WithSelectionModifierOrMiddleButton_PreservesWindowMode(
        MouseButton button,
        RawInputModifiers modifiers)
    {
        await DispatchAsync(async () =>
        {
            using PicaTemporaryDirectory directory = new();
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(TestTimeoutSeconds));
            ImageViewerWindow window = await CreateLoadedWindowAsync(
                directory.DirectoryPath, CreateBackgroundClickState(), timeout.Token);

            try
            {
                Point position = GetViewerClickPosition(window, true);

                DoubleClick(window, position, button, modifiers);

                window.CurrentWindowMode.Should().Be(ViewerWindowMode.Windowed);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task BackgroundDrag_FollowedBySingleClick_PreservesWindowMode()
    {
        await DispatchAsync(async () =>
        {
            using PicaTemporaryDirectory directory = new();
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(TestTimeoutSeconds));
            ImageViewerWindow window = await CreateLoadedWindowAsync(
                directory.DirectoryPath, CreateBackgroundClickState(), timeout.Token);

            try
            {
                const double DragDistance = 20d;
                Point start = GetViewerClickPosition(window, true);
                Point end = new(start.X + DragDistance, start.Y);

                window.MouseDown(start, MouseButton.Left, RawInputModifiers.None);
                window.MouseMove(end, RawInputModifiers.LeftMouseButton);
                window.MouseUp(end, MouseButton.Left, RawInputModifiers.None);
                window.MouseDown(end, MouseButton.Left, RawInputModifiers.None);
                window.MouseUp(end, MouseButton.Left, RawInputModifiers.None);

                window.CurrentWindowMode.Should().Be(ViewerWindowMode.Windowed);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task BackgroundDoubleClick_WithoutImage_PreservesWindowMode()
    {
        await DispatchAsync(() =>
        {
            ImageViewerWindow window = CreateWindow(
                CreateEmptyRequest(), CreateBackgroundClickState(), new RecordingImageChannelBitmapLoader());

            try
            {
                window.Show();
                Point position = new(window.ClientSize.Width / 2d, window.ClientSize.Height / 2d);

                DoubleClick(window, position);

                window.CurrentWindowMode.Should().Be(ViewerWindowMode.Windowed);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Theory]
    [InlineData(WindowResizeBehavior.Free, -1)]
    [InlineData(WindowResizeBehavior.Free, 0)]
    [InlineData(WindowResizeBehavior.Free, 1)]
    [InlineData(WindowResizeBehavior.FitWhenWindowed, -1)]
    [InlineData(WindowResizeBehavior.FitWhenWindowed, 0)]
    [InlineData(WindowResizeBehavior.FitWhenWindowed, 1)]
    [InlineData(WindowResizeBehavior.AlwaysFitImage, -1)]
    [InlineData(WindowResizeBehavior.AlwaysFitImage, 0)]
    [InlineData(WindowResizeBehavior.AlwaysFitImage, 1)]
    public async Task Resize_AtTopOfTitleBar_PreservesSizingBehaviorAndOppositeEdges(
        WindowResizeBehavior resizeBehavior,
        int horizontalDirection)
    {
        await DispatchAsync(async () =>
        {
            using PicaTemporaryDirectory directory = new();
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(TestTimeoutSeconds));
            ImageViewerState state = CreateWindowedState();
            state.IsFastLoadingEnabled = false;
            state.ResizeBehavior = resizeBehavior;
            ImageViewerWindow window = await CreateLoadedWindowAsync(directory.DirectoryPath, state, timeout.Token);
            ImageViewerView view = window.Content as ImageViewerView
                ?? throw new InvalidOperationException("The viewer content must be created.");
            const double EdgeInset = 1d;
            const double HorizontalDrag = 20d;
            const double VerticalDrag = 30d;

            try
            {
                PixelPoint initialPosition = window.Position;
                Size initialSize = window.ClientSize;
                double titleBarHeight = initialSize.Height - view.ViewerArea.Bounds.Height;
                double startX = horizontalDirection switch
                {
                    < 0 => EdgeInset,
                    > 0 => initialSize.Width - EdgeInset,
                    _ => initialSize.Width / 2d
                };
                Point start = new(startX, EdgeInset);
                Point end = new(start.X + (horizontalDirection * HorizontalDrag), start.Y + VerticalDrag);
                window.MouseMove(start, RawInputModifiers.None);

                window.MouseDown(start, MouseButton.Left, RawInputModifiers.None);
                window.MouseMove(end, RawInputModifiers.LeftMouseButton);
                window.MouseUp(end, MouseButton.Left, RawInputModifiers.None);
                window.UpdateLayout();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                window.CurrentWindowMode.Should().Be(ViewerWindowMode.Windowed);
                window.ClientSize.Height.Should().BeApproximately(initialSize.Height - VerticalDrag, 1d);
                window.Position.Y.Should().Be(initialPosition.Y + (int)VerticalDrag);
                (window.Position.Y + window.ClientSize.Height).Should().BeApproximately(
                    initialPosition.Y + initialSize.Height, 1d);

                if (horizontalDirection < 0)
                {
                    (window.Position.X + window.ClientSize.Width).Should().BeApproximately(
                        initialPosition.X + initialSize.Width, 1d);
                }
                else
                {
                    window.Position.X.Should().Be(initialPosition.X);
                }

                if (resizeBehavior == WindowResizeBehavior.AlwaysFitImage)
                {
                    double expectedWidth = (window.ClientSize.Height - titleBarHeight)
                        * SourceImageWidth / SourceImageHeight;
                    window.ClientSize.Width.Should().BeApproximately(expectedWidth, 1d);
                }
                else
                {
                    window.ClientSize.Width.Should().BeApproximately(
                        initialSize.Width + (Math.Abs(horizontalDirection) * HorizontalDrag), 1d);
                }

                view.Image.Width.Should().BeApproximately(
                    Math.Min(view.ViewerArea.Bounds.Width,
                        view.ViewerArea.Bounds.Height * SourceImageWidth / SourceImageHeight), 1d);
                view.Image.Height.Should().BeApproximately(view.Image.Width * SourceImageHeight / SourceImageWidth, 1d);
                double releasedHeight = window.Height;
                window.MouseMove(new Point(end.X, end.Y + VerticalDrag), RawInputModifiers.None);
                window.Height.Should().Be(releasedHeight);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Theory]
    [InlineData(300d, 240d, false, 1d)]
    [InlineData(300d, 240d, true, 1d)]
    [InlineData(640d, 480d, false, 17d)]
    [InlineData(900d, 700d, false, 17d)]
    [InlineData(640d, 480d, true, 17d)]
    [InlineData(900d, 700d, true, 17d)]
    public async Task WindowResizeOverlay_InWindowedMode_CoversWindowEdgesAndLeavesTitleBarInteractive(
        double width,
        double height,
        bool autoHide,
        double topEdgeInset)
    {
        await DispatchAsync(async () =>
        {
            ImageViewerState state = CreateWindowedState();
            state.ResizeBehavior = WindowResizeBehavior.Free;
            state.WindowWidth = width;
            state.WindowHeight = height;
            state.AutoHideWindowTitleBar = autoHide;
            ImageViewerWindow window = CreateWindow(CreateEmptyRequest(), state, new RecordingImageChannelBitmapLoader());
            ImageViewerView view = window.Content as ImageViewerView
                ?? throw new InvalidOperationException("The viewer content must be created.");
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(TestTimeoutSeconds));
            const double EdgeInset = 17d;
            const double CornerInset = 35d;
            const double CornerEdgeInset = 1d;
            const double ButtonEdgeInset = 3d;

            try
            {
                window.Show();
                window.UpdateLayout();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.Background);
                Size size = window.ClientSize;
                Control titleBar = window.GetVisualDescendants().OfType<Control>()
                    .Single(control => control.Name == "PART_TitleBar");
                double titleBarHeight = titleBar.Bounds.Height;
                (Point Position, WindowSizingEdges Edges)[] resizePoints =
                [
                    (new Point(EdgeInset, size.Height / 2d), WindowSizingEdges.Left),
                    (new Point(size.Width - EdgeInset, size.Height / 2d), WindowSizingEdges.Right),
                    (new Point(size.Width / 2d, topEdgeInset), WindowSizingEdges.Top),
                    (new Point(size.Width / 2d, size.Height - EdgeInset), WindowSizingEdges.Bottom),
                    (new Point(CornerInset, CornerEdgeInset), WindowSizingEdges.TopLeft),
                    (new Point(size.Width - CornerInset, CornerEdgeInset), WindowSizingEdges.TopRight),
                    (new Point(CornerInset, size.Height - CornerEdgeInset), WindowSizingEdges.BottomLeft),
                    (new Point(size.Width - CornerInset, size.Height - CornerEdgeInset), WindowSizingEdges.BottomRight),
                    (new Point(CornerEdgeInset, CornerInset), WindowSizingEdges.TopLeft),
                    (new Point(size.Width - CornerEdgeInset, CornerInset), WindowSizingEdges.TopRight),
                    (new Point(CornerEdgeInset, size.Height - CornerInset), WindowSizingEdges.BottomLeft),
                    (new Point(size.Width - CornerEdgeInset, size.Height - CornerInset), WindowSizingEdges.BottomRight)
                ];

                foreach ((Point position, WindowSizingEdges edges) in resizePoints)
                {
                    AssertResizeEdge(window, position, edges);
                }

                Point titleBarCenter = new(size.Width / 2d, titleBarHeight / 2d);
                window.MouseMove(titleBarCenter, RawInputModifiers.None);
                await WaitForOpacityAsync(titleBar, 1d, timeout.Token);
                Visual titleBarHit = window.InputHitTest(titleBarCenter)
                    .Should().BeAssignableTo<Visual>().Subject;
                titleBarHit.GetVisualAncestors().Should().NotContain(view.WindowResizeOverlay);
                Point titleBarOrigin = titleBar.TranslatePoint(default, window)
                    ?? throw new InvalidOperationException("The title bar must be attached to the window.");
                titleBarOrigin.Should().Be(default(Point));
                titleBar.Bounds.Width.Should().BeApproximately(size.Width, 0.01d);
                Control logo = titleBar.GetVisualDescendants().OfType<Control>()
                    .Single(control => control.Name == "PART_Logo");
                Point logoCenter = logo.TranslatePoint(
                    new Point(logo.Bounds.Width / 2d, logo.Bounds.Height / 2d), window)
                    ?? throw new InvalidOperationException("The title bar logo must be attached to the window.");
                Visual logoHit = window.InputHitTest(logoCenter).Should().BeAssignableTo<Visual>().Subject;
                logoHit.GetVisualAncestors().Prepend(logoHit).Should().NotContain(logo);

                Button[] titleBarButtons = titleBar.GetVisualDescendants().OfType<Button>()
                    .Where(button => button.IsEffectivelyVisible)
                    .ToArray();

                foreach (Button button in titleBarButtons)
                {
                    button.Bounds.Width.Should().BeApproximately(44d, 0.01d);
                }

                Rect[] buttonBounds = titleBarButtons
                    .Select(button =>
                    {
                        Point origin = button.TranslatePoint(default, window)
                            ?? throw new InvalidOperationException("The title bar button must be attached to the window.");

                        return new Rect(origin, button.Bounds.Size);
                    })
                    .OrderBy(bounds => bounds.Left)
                    .ToArray();
                buttonBounds.Should().HaveCount(5);

                for (int i = 0; i < buttonBounds.Length; i++)
                {
                    buttonBounds[i].Top.Should().BeApproximately(titleBarOrigin.Y, 0.01d);
                    buttonBounds[i].Height.Should().BeApproximately(titleBarHeight, 0.01d);
                    buttonBounds[i].Left.Should().BeGreaterThanOrEqualTo(0d);
                    buttonBounds[i].Right.Should().BeLessThanOrEqualTo(size.Width);

                    if (i > 0)
                    {
                        buttonBounds[i].Left.Should().BeApproximately(buttonBounds[i - 1].Right, 0.01d);
                    }
                }

                foreach (Control button in window.GetVisualDescendants().OfType<Button>()
                    .Where(button => button.Name is "PART_CloseButton" or "PART_MinimizeButton" or "PART_PinButton")
                    .Cast<Control>().Concat(window.RightWindowTitleBarControls))
                {
                    Point[] buttonPoints =
                    [
                        new Point(button.Bounds.Width / 2d, button.Bounds.Height / 2d),
                        new Point(button.Bounds.Width / 2d, ButtonEdgeInset),
                        new Point(ButtonEdgeInset, button.Bounds.Height / 2d),
                        new Point(button.Bounds.Width - ButtonEdgeInset, button.Bounds.Height / 2d),
                        new Point(button.Bounds.Width / 2d, button.Bounds.Height - CornerEdgeInset)
                    ];

                    foreach (Point buttonPoint in buttonPoints)
                    {
                        Point point = button.TranslatePoint(buttonPoint, window)
                            ?? throw new InvalidOperationException("The title bar button must be attached to the window.");
                        Visual hit = window.InputHitTest(point).Should().BeAssignableTo<Visual>().Subject;
                        hit.GetVisualAncestors().Prepend(hit).Should().Contain(button);
                    }
                }

                Button settingsButton = window.RightWindowTitleBarControls.Single()
                    .Should().BeOfType<Button>().Subject;
                Point settingsCenter = settingsButton.TranslatePoint(
                    new Point(settingsButton.Bounds.Width / 2d, settingsButton.Bounds.Height / 2d), window)
                    ?? throw new InvalidOperationException("The settings button must be attached to the window.");
                Button pinButton = titleBarButtons.Single(button => button.Name == "PART_PinButton");
                Button minimizeButton = titleBarButtons.Single(button => button.Name == "PART_MinimizeButton");
                Button fullScreenButton = titleBarButtons.Single(button => button.Name == "PART_FullScreenButton");
                pinButton.Bounds.Right.Should().BeApproximately(minimizeButton.Bounds.Left, 0.01d);
                minimizeButton.Bounds.Right.Should().BeApproximately(fullScreenButton.Bounds.Left, 0.01d);
                Point pinCenter = pinButton.TranslatePoint(
                    new Point(pinButton.Bounds.Width / 2d, pinButton.Bounds.Height / 2d), window)
                    ?? throw new InvalidOperationException("The pin button must be attached to the window.");
                window.MouseMove(pinCenter, RawInputModifiers.None);
                IBrush? hoverBackground = pinButton.GetBaseValue(TemplatedControl.BackgroundProperty).Value;
                window.MouseMove(settingsCenter, RawInputModifiers.None);
                settingsButton.GetBaseValue(TemplatedControl.BackgroundProperty).Value.Should().Be(hoverBackground);
                settingsButton.BorderThickness.Should().Be(pinButton.BorderThickness);
                BrushTransition settingsTransition = settingsButton.Transitions.Should().ContainSingle().Which
                    .Should().BeOfType<BrushTransition>().Subject;
                BrushTransition pinTransition = pinButton.Transitions.Should().ContainSingle().Which
                    .Should().BeOfType<BrushTransition>().Subject;
                settingsTransition.Property.Should().Be(pinTransition.Property);
                settingsTransition.Duration.Should().Be(pinTransition.Duration);
                window.MouseDown(settingsCenter, MouseButton.Left, RawInputModifiers.None);
                window.MouseUp(settingsCenter, MouseButton.Left, RawInputModifiers.None);
                view.SettingsPanel.IsVisible.Should().BeTrue();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task WindowResizeOverlay_AfterFullScreenRoundTrip_RestoresWindowEdges()
    {
        await DispatchAsync(() =>
        {
            ImageViewerWindow window = CreateWindow(CreateEmptyRequest(), CreateWindowedState(),
                new RecordingImageChannelBitmapLoader());
            ImageViewerView view = window.Content as ImageViewerView
                ?? throw new InvalidOperationException("The viewer content must be created.");

            try
            {
                window.Show();

                window.WindowState = WindowState.FullScreen;
                window.UpdateLayout();

                view.WindowResizeOverlay.IsVisible.Should().BeFalse();
                Visual fullScreenHit = window.InputHitTest(new Point(window.ClientSize.Width / 2d, 1d))
                    .Should().BeAssignableTo<Visual>().Subject;
                fullScreenHit.GetVisualAncestors().Should().NotContain(view.WindowResizeOverlay);

                window.WindowState = WindowState.Normal;
                window.UpdateLayout();

                view.WindowResizeOverlay.IsVisible.Should().BeTrue();
                AssertResizeEdge(window, new Point(window.ClientSize.Width / 2d, 1d), WindowSizingEdges.Top);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task WindowResizeOverlay_AfterTemplateReapplied_ReusesSingleOverlay()
    {
        await DispatchAsync(async () =>
        {
            ImageViewerWindow window = CreateWindow(CreateEmptyRequest(), CreateWindowedState(),
                new RecordingImageChannelBitmapLoader());
            ImageViewerView view = window.Content as ImageViewerView
                ?? throw new InvalidOperationException("The viewer content must be created.");
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(TestTimeoutSeconds));

            try
            {
                window.Show();
                IControlTemplate template = window.Template
                    ?? throw new InvalidOperationException("The window template must be applied.");
                Panel originalParent = view.WindowResizeOverlay.Parent.Should().BeAssignableTo<Panel>().Subject;
                window.MouseMove(new Point(window.ClientSize.Width / 2d, 20d), RawInputModifiers.None);

                window.Template = null;
                window.ApplyTemplate();
                window.Template = template;
                window.ApplyTemplate();
                window.UpdateLayout();

                originalParent.Children.Should().NotContain(view.WindowResizeOverlay);
                window.GetVisualDescendants().Should().ContainSingle(visual => ReferenceEquals(visual, view.WindowResizeOverlay));
                Control titleBar = window.GetVisualDescendants().OfType<Control>()
                    .Single(control => control.Name == "PART_TitleBar");
                window.MouseMove(new Point(window.ClientSize.Width / 2d, titleBar.Bounds.Height / 2d), RawInputModifiers.None);
                await WaitForOpacityAsync(titleBar, 1d, timeout.Token);
                titleBar.Opacity.Should().Be(1d);
                view.ViewerArea.Bounds.Size.Should().Be(window.ClientSize);
                AssertResizeEdge(window, new Point(window.ClientSize.Width / 2d, 1d), WindowSizingEdges.Top);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task TitleBarDoubleClick_OnSettingsButton_KeepsWindowedMode()
    {
        await DispatchAsync(() =>
        {
            ImageViewerWindow window = CreateWindow(
                CreateEmptyRequest(),
                CreateWindowedState(),
                new RecordingImageChannelBitmapLoader());

            try
            {
                window.Show();
                Control settingsButton = window
                    .RightWindowTitleBarControls
                    .Single();
                Point settingsButtonCenter = settingsButton.TranslatePoint(
                    new Point(
                        settingsButton.Bounds.Width / 2d,
                        settingsButton.Bounds.Height / 2d),
                    window)
                    ?? throw new InvalidOperationException(
                        "The title bar settings button is not attached to the test window.");

                DoubleClick(window, settingsButtonCenter);

                window.CurrentWindowMode
                    .Should()
                    .Be(ViewerWindowMode.Windowed);
                window.WindowState.Should().Be(WindowState.Normal);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task ContextMenu_WhenActionStateChanges_RefreshesExternalActionLabel()
    {
        await DispatchAsync(async () =>
        {
            using PicaTemporaryDirectory temporaryDirectory = new();
            string imagePath = await CreateImageAsync(
                temporaryDirectory.DirectoryPath);
            PicaImageItem item = new(ItemId, imagePath, "image.png");
            PicaActionDefinition action = new(
                "test.favorite",
                "Favorite",
                ViewerActionIconGeometry.Star,
                0d,
                PicaActionTargets.CurrentImage,
                0);
            PicaViewerRequest request = new(
                new List<PicaImageItem> { item },
                ItemId,
                new List<PicaActionDefinition> { action });
            bool isFavorite = true;
            RecordingViewerActionDispatcher actionDispatcher = new()
            {
                DisplayNameResolver = (_, currentItem) =>
                {
                    currentItem.Id.Should().Be(ItemId);
                    return isFavorite ? "Remove favorite" : "Favorite";
                }
            };
            ImageViewerWindow window = CreateWindow(
                request,
                new ImageViewerState(),
                new RecordingImageChannelBitmapLoader(),
                actionDispatcher: actionDispatcher);
            ImageViewerView view = window.Content as ImageViewerView
                ?? throw new InvalidOperationException(
                    "The viewer content must be created.");
            TaskCompletionSource imageLoaded = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            view.Image.PropertyChanged += (_, e) =>
            {
                if ((e.Property == Image.SourceProperty)
                    && (view.Image.Source is Bitmap))
                {
                    imageLoaded.TrySetResult();
                }
            };

            try
            {
                window.Show();
                await imageLoaded.Task.WaitAsync(
                    TimeSpan.FromSeconds(TestTimeoutSeconds));
                Button button = view.ViewerContextMenu
                    .GetVisualDescendants()
                    .OfType<Button>()
                    .Single(candidate => ReferenceEquals(candidate.Tag, action));
                StackPanel content = button.Content.Should()
                    .BeOfType<StackPanel>().Subject;
                TextBlock label = content.Children
                    .OfType<TextBlock>()
                    .Single();

                window.MouseDown(
                    new Point(100d, 100d),
                    MouseButton.Right,
                    RawInputModifiers.None);

                label.Text.Should().Be("Remove favorite");

                isFavorite = false;
                window.MouseDown(
                    new Point(100d, 100d),
                    MouseButton.Right,
                    RawInputModifiers.None);

                label.Text.Should().Be("Favorite");
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task ContextMenuButton_Click_HidesContextMenu()
    {
        await DispatchAsync(async () =>
        {
            ImageViewerWindow window = CreateWindow(
                CreateEmptyRequest(),
                new ImageViewerState(),
                new RecordingImageChannelBitmapLoader());
            ImageViewerView view = window.Content as ImageViewerView
                ?? throw new InvalidOperationException(
                    "The viewer content must be created.");

            try
            {
                window.Show();
                window.MouseDown(
                    new Point(100d, 100d),
                    MouseButton.Right,
                    RawInputModifiers.None);

                view.ViewerContextMenu.IsVisible.Should().BeTrue();
                view.ViewerContextMenu.IsHitTestVisible.Should().BeTrue();
                view.ViewerContextMenu.Clip.Should().NotBeNull();
                await Task.Delay(
                    ViewerFloatingMenuAnimator.OpacityRevealDurationMilliseconds + 20);
                view.ViewerContextMenu.Clip.Should().NotBeNull();
                Button menuButton = view.ViewerContextMenu
                    .GetVisualDescendants()
                    .OfType<Button>()
                    .First();
                Point buttonPosition = menuButton.TranslatePoint(
                    new Point(
                        menuButton.Bounds.Width / 2d,
                        menuButton.Bounds.Height / 2d),
                    window)
                    ?? throw new InvalidOperationException(
                        "The context menu button is not attached to the viewer window.");

                window.MouseDown(
                    buttonPosition,
                    MouseButton.Left,
                    RawInputModifiers.None);
                window.MouseUp(
                    buttonPosition,
                    MouseButton.Left,
                    RawInputModifiers.None);

                view.ViewerContextMenu.IsHitTestVisible.Should().BeFalse();
                view.ViewerContextMenu.IsVisible.Should().BeTrue();
                await Task.Delay(
                    ViewerFloatingMenuAnimator.ClosingDurationMilliseconds + 50);
                view.ViewerContextMenu.IsVisible.Should().BeFalse();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SetSavingInteractionState_WhileSaving_BlocksCloseAndImageControls(
        bool isWindowed)
    {
        await DispatchAsync(() =>
        {
            ImageViewerState state = isWindowed
                ? CreateWindowedState()
                : new ImageViewerState();
            ImageViewerWindow window = CreateWindow(
                CreateEmptyRequest(),
                state,
                new RecordingImageChannelBitmapLoader());
            bool wasClosed = false;
            window.Closed += (_, _) => wasClosed = true;

            try
            {
                window.Show();
                ImageViewerView view = window.Content as ImageViewerView
                    ?? throw new InvalidOperationException(
                        "The viewer content must be created.");
                Button titleBarClose = window.GetVisualDescendants()
                    .OfType<Button>()
                    .Single(button => button.Name == "PART_CloseButton");
                Button titleBarMinimize = window.GetVisualDescendants()
                    .OfType<Button>()
                    .Single(button => button.Name == "PART_MinimizeButton");
                Button titleBarPin = window.GetVisualDescendants()
                    .OfType<Button>()
                    .Single(button => button.Name == "PART_PinButton");
                Button titleBarFullScreen = window.GetVisualDescendants()
                    .OfType<Button>()
                    .Single(button => button.Name == "PART_FullScreenButton");

                window.SetSavingInteractionState(true);
                window.Close();

                window.IsSaving.Should().BeTrue();
                wasClosed.Should().BeFalse();
                window.Content.Should().BeSameAs(view);
                view.CloseButton.IsEnabled.Should().BeFalse();
                titleBarClose.IsEnabled.Should().BeFalse();
                titleBarMinimize.IsEnabled.Should().BeFalse();
                titleBarPin.IsEnabled.Should().BeFalse();
                titleBarFullScreen.IsEnabled.Should().BeTrue();
                view.BottomControls.IsEnabled.Should().BeFalse();
                view.ContentNavigationPanel.IsEnabled.Should().BeFalse();
                view.SelectionToolbar.IsEnabled.Should().BeFalse();
                view.LeftNavigationArea.IsEnabled.Should().BeFalse();
                view.RightNavigationArea.IsEnabled.Should().BeFalse();
                view.WindowModeButton.IsEnabled.Should().BeTrue();
                view.FullscreenSettingsButton.IsEnabled.Should().BeTrue();
                view.TitleBarSettingsControls.Should()
                    .OnlyContain(control => control.IsEnabled);

                window.SetSavingInteractionState(false);

                window.IsSaving.Should().BeFalse();
                view.CloseButton.IsEnabled.Should().BeTrue();
                titleBarClose.IsEnabled.Should().BeTrue();
                titleBarMinimize.IsEnabled.Should().BeTrue();
                titleBarPin.IsEnabled.Should().BeTrue();
                titleBarFullScreen.IsEnabled.Should().BeTrue();
                view.BottomControls.IsEnabled.Should().BeTrue();
                view.ContentNavigationPanel.IsEnabled.Should().BeTrue();
                view.SelectionToolbar.IsEnabled.Should().BeTrue();
                view.LeftNavigationArea.IsEnabled.Should().BeTrue();
                view.RightNavigationArea.IsEnabled.Should().BeTrue();
            }
            finally
            {
                window.SetSavingInteractionState(false);
                window.Close();
            }

            wasClosed.Should().BeTrue();
        });
    }

    [Fact]
    public async Task SetSavingInteractionState_WhileSaving_AllowsSettingsAndWindowMode()
    {
        await DispatchAsync(() =>
        {
            ImageViewerWindow window = CreateWindow();

            try
            {
                window.Show();
                ImageViewerView view = window.Content as ImageViewerView
                    ?? throw new InvalidOperationException(
                        "The viewer content must be created.");
                window.SetSavingInteractionState(true);

                view.FullscreenSettingsButton.RaiseEvent(
                    new RoutedEventArgs(Button.ClickEvent));
                view.SettingsPanel.IsVisible.Should().BeTrue();

                view.WindowModeButton.RaiseEvent(
                    new RoutedEventArgs(Button.ClickEvent));

                window.CurrentWindowMode.Should().Be(
                    ViewerWindowMode.Windowed);
                window.IsSaving.Should().BeTrue();
                view.CloseButton.IsEnabled.Should().BeFalse();
            }
            finally
            {
                window.SetSavingInteractionState(false);
                window.Close();
            }
        });
    }

    [Fact]
    public async Task Constructor_WithSaveStatus_FadesInAndOutWithSaveState()
    {
        await DispatchAsync(async () =>
        {
            ImageViewerWindow window = CreateWindow(
                CreateEmptyRequest(),
                CreateWindowedState(),
                new RecordingImageChannelBitmapLoader());

            try
            {
                window.Show();
                ImageViewerView view = window.Content as ImageViewerView
                    ?? throw new InvalidOperationException(
                        "The viewer content must be created.");
                ImageViewerActionsViewModel actions = view.SaveStatus.DataContext
                    .Should().BeOfType<ImageViewerActionsViewModel>().Subject;

                view.SaveStatus.IsVisible.Should().BeFalse();
                view.SaveStatus.IsHitTestVisible.Should().BeFalse();
                view.SaveStatusPanelTransform.Y.Should().BeGreaterThan(0d);

                actions.IsSaveTakingLong = true;

                view.SaveStatus.IsVisible.Should().BeTrue();
                view.SaveStatus.Opacity.Should().Be(0d);
                await Task.Delay(270);
                view.SaveStatus.Opacity.Should().BeApproximately(1d, 0.01d);
                view.SaveStatusPanelTransform.Y.Should().BeApproximately(0d, 0.1d);

                actions.IsSaveTakingLong = false;

                view.SaveStatus.IsVisible.Should().BeTrue();
                await Task.Delay(270);
                view.SaveStatus.IsVisible.Should().BeFalse();
                view.SaveStatus.Opacity.Should().BeApproximately(0d, 0.01d);
                view.SaveStatusPanelTransform.Y.Should().BeGreaterThan(0d);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task Constructor_WhenSaveRestartsDuringFade_KeepsStatusVisible()
    {
        await DispatchAsync(async () =>
        {
            ImageViewerWindow window = CreateWindow(
                CreateEmptyRequest(),
                CreateWindowedState(),
                new RecordingImageChannelBitmapLoader());

            try
            {
                window.Show();
                ImageViewerView view = window.Content as ImageViewerView
                    ?? throw new InvalidOperationException(
                        "The viewer content must be created.");
                ImageViewerActionsViewModel actions = view.SaveStatus.DataContext
                    .Should().BeOfType<ImageViewerActionsViewModel>().Subject;

                actions.IsSaveTakingLong = true;
                await Task.Delay(80);
                actions.IsSaveTakingLong = false;
                actions.IsSaveTakingLong = true;
                await Task.Delay(270);

                view.SaveStatus.IsVisible.Should().BeTrue();
                view.SaveStatus.Opacity.Should().BeApproximately(1d, 0.01d);
                view.SaveStatusPanelTransform.Y.Should().BeApproximately(0d, 0.1d);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task Constructor_WithSaveStatus_DimsContentAndCentersLargerPanel()
    {
        await DispatchAsync(() =>
        {
            ImageViewerWindow window = CreateWindow(
                CreateEmptyRequest(),
                CreateWindowedState(),
                new RecordingImageChannelBitmapLoader());

            try
            {
                window.Show();
                ImageViewerView view = window.Content as ImageViewerView
                    ?? throw new InvalidOperationException(
                        "The viewer content must be created.");
                ImageViewerActionsViewModel actions = view.SaveStatus.DataContext
                    .Should().BeOfType<ImageViewerActionsViewModel>().Subject;

                actions.IsSaveTakingLong = true;
                view.UpdateLayout();

                Border panel = view.SaveStatus.Child
                    .Should().BeOfType<Border>().Subject;
                StackPanel content = panel.Child
                    .Should().BeOfType<StackPanel>().Subject;
                SolidColorBrush dimBrush = view.SaveStatus.Background
                    .Should().BeOfType<SolidColorBrush>().Subject;

                view.Root.Children.Should().Contain(view.SaveStatus);
                view.SaveStatus.ZIndex.Should().BeGreaterThan(
                    view.SettingsPanel.ZIndex);
                view.SaveStatus.Bounds.Size.Should().Be(view.Root.Bounds.Size);
                dimBrush.Color.Should().Be(Color.Parse("#40000000"));
                panel.HorizontalAlignment.Should().Be(HorizontalAlignment.Center);
                panel.VerticalAlignment.Should().Be(VerticalAlignment.Center);
                panel.MinWidth.Should().BeGreaterThan(200d);
                content.Children[0].Width.Should().BeGreaterThan(18d);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task BottomMenus_WhenOpened_RevealAndFadeOnDismiss()
    {
        await DispatchAsync(async () =>
        {
            ImageViewerWindow window = CreateWindow(
                CreateEmptyRequest(),
                new ImageViewerState(),
                new RecordingImageChannelBitmapLoader());
            ImageViewerView view = window.Content as ImageViewerView
                ?? throw new InvalidOperationException(
                    "The viewer content must be created.");

            try
            {
                window.Show();
                view.ToolMenuButton.RaiseEvent(
                    new RoutedEventArgs(Button.ClickEvent));

                view.ToolMenu.IsVisible.Should().BeTrue();
                view.ToolMenu.IsHitTestVisible.Should().BeTrue();
                view.ToolMenu.Clip.Should().NotBeNull();
                window.CaptureRenderedFrame();

                view.ModeMenuButton.RaiseEvent(
                    new RoutedEventArgs(Button.ClickEvent));

                view.ModeMenu.IsVisible.Should().BeTrue();
                view.ModeMenu.IsHitTestVisible.Should().BeTrue();
                view.ModeMenu.Clip.Should().NotBeNull();

                view.ToolMenuButton.RaiseEvent(
                    new RoutedEventArgs(Button.ClickEvent));

                view.ToolMenu.IsHitTestVisible.Should().BeFalse();
                view.ModeMenu.IsHitTestVisible.Should().BeFalse();
                await Task.Delay(
                    ViewerFloatingMenuAnimator.ClosingDurationMilliseconds + 50);
                view.ToolMenu.IsVisible.Should().BeFalse();
                view.ModeMenu.IsVisible.Should().BeFalse();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task ToolMenu_WhenReopenedDuringClose_RemainsInteractive()
    {
        await DispatchAsync(async () =>
        {
            ImageViewerWindow window = CreateWindow(
                CreateEmptyRequest(),
                new ImageViewerState(),
                new RecordingImageChannelBitmapLoader());
            ImageViewerView view = window.Content as ImageViewerView
                ?? throw new InvalidOperationException(
                    "The viewer content must be created.");

            try
            {
                window.Show();
                view.ToolMenuButton.RaiseEvent(
                    new RoutedEventArgs(Button.ClickEvent));
                view.ToolMenuButton.RaiseEvent(
                    new RoutedEventArgs(Button.ClickEvent));
                view.ToolMenuButton.RaiseEvent(
                    new RoutedEventArgs(Button.ClickEvent));
                await Task.Delay(
                    ViewerFloatingMenuAnimator.ClosingDurationMilliseconds + 50);

                view.ToolMenu.IsVisible.Should().BeTrue();
                view.ToolMenu.IsHitTestVisible.Should().BeTrue();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task OpenWithSubmenu_WhenOpenedFromContextMenu_RevealsAndFadesOnDismiss()
    {
        await DispatchAsync(async () =>
        {
            using PicaTemporaryDirectory temporaryDirectory = new();
            string imagePath = await CreateImageAsync(
                temporaryDirectory.DirectoryPath);
            PicaImageItem item = new(ItemId, imagePath, "image.png");
            PicaViewerRequest request = new(
                new List<PicaImageItem> { item },
                ItemId);
            RecordingPlatformFileActions fileActions = new();
            ImageViewerWindow window = CreateWindow(
                request,
                new ImageViewerState(),
                new RecordingImageChannelBitmapLoader(),
                fileActions);
            ImageViewerView view = window.Content as ImageViewerView
                ?? throw new InvalidOperationException(
                    "The viewer content must be created.");
            TaskCompletionSource imageLoaded = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            view.Image.PropertyChanged += (_, e) =>
            {
                if ((e.Property == Image.SourceProperty)
                    && (view.Image.Source is Bitmap))
                {
                    imageLoaded.TrySetResult();
                }
            };

            try
            {
                window.Show();
                await imageLoaded.Task.WaitAsync(
                    TimeSpan.FromSeconds(TestTimeoutSeconds));
                window.MouseDown(
                    new Point(100d, 100d),
                    MouseButton.Right,
                    RawInputModifiers.None);
                view.ContextOpenWithButton.RaiseEvent(
                    new RoutedEventArgs(Button.ClickEvent));

                view.OpenWithMenu.IsVisible.Should().BeTrue();
                view.OpenWithMenu.IsHitTestVisible.Should().BeTrue();
                view.OpenWithMenu.Clip.Should().NotBeNull();
                fileActions.LoadApplicationsCount.Should().Be(1);

                window.MouseDown(
                    new Point(400d, 400d),
                    MouseButton.Left,
                    RawInputModifiers.None);

                view.OpenWithMenu.IsHitTestVisible.Should().BeFalse();
                view.OpenWithMenu.IsVisible.Should().BeTrue();
                await Task.Delay(
                    ViewerFloatingMenuAnimator.ClosingDurationMilliseconds + 50);
                view.OpenWithMenu.IsVisible.Should().BeFalse();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task OnKeyDown_WithLoadedImage_TogglesCheckerboardLayerWithoutReplacingSource()
    {
        await DispatchAsync(async () =>
        {
            using PicaTemporaryDirectory temporaryDirectory = new();
            string imagePath = await CreateImageAsync(
                temporaryDirectory.DirectoryPath);
            PicaImageItem item = new(
                ItemId,
                imagePath,
                "image.png");
            PicaViewerRequest request = new(
                new List<PicaImageItem> { item },
                ItemId);
            ImageViewerState state = new()
            {
                IsCheckerboardBackgroundEnabled = false,
                IsFastLoadingEnabled = false
            };
            ImageViewerWindow window = CreateWindow(
                request,
                state,
                new RecordingImageChannelBitmapLoader());
            ImageViewerView view = window.Content as ImageViewerView
                ?? throw new InvalidOperationException(
                    "The viewer content must be created.");
            TaskCompletionSource<Bitmap> loadedSource = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            view.Image.PropertyChanged += (_, e) =>
            {
                if ((e.Property == Image.SourceProperty)
                    && (view.Image.Source is Bitmap source))
                {
                    loadedSource.TrySetResult(source);
                }
            };

            try
            {
                window.Show();
                Bitmap source = await loadedSource.Task.WaitAsync(
                    TimeSpan.FromSeconds(TestTimeoutSeconds));

                window.KeyPress(
                    Key.T,
                    RawInputModifiers.None,
                    PhysicalKey.T,
                    null);

                view.CheckerboardBackground.IsVisible.Should().BeTrue();
                view.Image.Source.Should().BeSameAs(source);
                view.CheckerboardBackground.Width.Should().Be(
                    view.Image.Width);
                view.CheckerboardBackground.Height.Should().Be(
                    view.Image.Height);
                Canvas.GetLeft(view.CheckerboardBackground).Should().Be(
                    Canvas.GetLeft(view.Image));
                Canvas.GetTop(view.CheckerboardBackground).Should().Be(
                    Canvas.GetTop(view.Image));

                window.KeyPress(
                    Key.T,
                    RawInputModifiers.None,
                    PhysicalKey.T,
                    null);

                view.CheckerboardBackground.IsVisible.Should().BeFalse();
                view.Image.Source.Should().BeSameAs(source);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Navigate_WithSameResolutionAndPreservationEnabled_KeepsZoomAndPosition(
        bool fastLoading)
    {
        await DispatchAsync(async () =>
        {
            using PicaTemporaryDirectory temporaryDirectory = new();
            string firstPath = await CreateImageAsync(
                temporaryDirectory.DirectoryPath);
            string secondPath = Path.Combine(
                temporaryDirectory.DirectoryPath,
                "second.png");
            File.Copy(firstPath, secondPath);
            Guid secondItemId = Guid.Parse(
                "22222222-2222-2222-2222-222222222222");
            PicaViewerRequest request = new(
                new List<PicaImageItem>
                {
                    new(ItemId, firstPath, "image.png"),
                    new(secondItemId, secondPath, "second.png")
                },
                ItemId);
            ControlledFullResolutionImageLoader fullResolutionLoader = new(
                new List<string> { firstPath, secondPath });
            ImageViewerState state = new()
            {
                IsFastLoadingEnabled = fastLoading,
                IsPanningInertiaEnabled = false,
                PreserveZoomAndPositionOnNavigation = true,
                ResizeBehavior = WindowResizeBehavior.Free
            };
            ImageViewerWindow window = CreateWindow(
                request,
                state,
                new RecordingImageChannelBitmapLoader(),
                new ImagePreviewLoader(
                    new ImageFormatRegistry(),
                    NullLogger<ImagePreviewLoader>.Instance),
                fullResolutionLoader);
            ImageViewerView view = window.Content as ImageViewerView
                ?? throw new InvalidOperationException(
                    "The viewer content must be created.");
            using CancellationTokenSource timeout = new(
                TimeSpan.FromSeconds(TestTimeoutSeconds));
            Bitmap firstBitmap = CreateBitmap(SourceImageWidth, SourceImageHeight);
            Bitmap secondBitmap = CreateBitmap(SourceImageWidth, SourceImageHeight);

            try
            {
                window.Show();
                await fullResolutionLoader.WaitUntilStartedAsync(
                    firstPath,
                    timeout.Token);
                fullResolutionLoader.Complete(firstPath, firstBitmap);
                await WaitForImageSourceAsync(view, firstBitmap, timeout.Token);

                Button zoomInButton = view.FindControl<Button>("ZoomInButton")
                    ?? throw new InvalidOperationException(
                        "The viewer zoom button was not found.");
                zoomInButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await Task.Delay(250, timeout.Token);
                Point panStart = new(
                    view.ViewerArea.Bounds.Width / 2d,
                    view.ViewerArea.Bounds.Height / 2d);
                Point panEnd = new(panStart.X + 80d, panStart.Y + 40d);
                window.MouseDown(
                    panStart,
                    MouseButton.Left,
                    RawInputModifiers.None);
                window.MouseMove(
                    panEnd,
                    RawInputModifiers.LeftMouseButton);
                window.MouseUp(
                    panEnd,
                    MouseButton.Left,
                    RawInputModifiers.None);
                await Task.Delay(250, timeout.Token);
                double expectedWidth = view.Image.Width;
                double expectedHeight = view.Image.Height;
                double expectedLeft = Canvas.GetLeft(view.Image);
                double expectedTop = Canvas.GetTop(view.Image);

                window.KeyPress(
                    Key.Right,
                    RawInputModifiers.None,
                    PhysicalKey.ArrowRight,
                    null);
                if (fastLoading)
                {
                    await WaitForImageSourceAsync(
                        view,
                        source => source.PixelSize.Width
                            == ImagePreviewLoader.PreviewDecodeWidth,
                        timeout.Token);

                    view.Image.Width.Should().BeApproximately(
                        expectedWidth,
                        0.001d);
                    Canvas.GetLeft(view.Image).Should().BeApproximately(
                        expectedLeft,
                        0.001d);
                    Canvas.GetTop(view.Image).Should().BeApproximately(
                        expectedTop,
                        0.001d);
                }

                await fullResolutionLoader.WaitUntilStartedAsync(
                    secondPath,
                    timeout.Token);
                fullResolutionLoader.Complete(secondPath, secondBitmap);
                await WaitForImageSourceAsync(view, secondBitmap, timeout.Token);

                view.Image.Width.Should().BeApproximately(expectedWidth, 0.001d);
                view.Image.Height.Should().BeApproximately(expectedHeight, 0.001d);
                Canvas.GetLeft(view.Image).Should().BeApproximately(
                    expectedLeft,
                    0.001d);
                Canvas.GetTop(view.Image).Should().BeApproximately(
                    expectedTop,
                    0.001d);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task PasteFromClipboardAsync_WithRepeatedPastesAndNavigationControls_RestoresOriginalAndResetsPlacement()
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(typeof(SkiaViewerTestSession), SessionLock, async () =>
        {
            using PicaTemporaryDirectory directory = new();
            string originalPath = await CreateImageAsync(directory.DirectoryPath);
            PicaImageItem original = new(ItemId, originalPath, "original.png");
            PicaViewerRequest request = new(new PicaImageItem[] { original }, ItemId);
            DelegateClipboardImageReader reader = new()
            {
                Read = _ => Task.FromResult<IReadOnlyList<ClipboardImageInput>>(
                    new ClipboardImageInput[] { ClipboardImageInput.FromBitmap(CreateBitmap(SourceImageWidth, SourceImageHeight)) })
            };
            ImageViewerState viewerState = new()
            {
                IsFastLoadingEnabled = false,
                IsPanningInertiaEnabled = false,
                PreserveZoomAndPositionOnNavigation = true,
                ResizeBehavior = WindowResizeBehavior.Free
            };
            ImageViewerWindow window = CreateWindow(request, viewerState,
                new RecordingImageChannelBitmapLoader(), clipboardReader: reader);
            ImageViewerView view = window.Content as ImageViewerView
                ?? throw new InvalidOperationException("The viewer content must be created.");
            ImageViewerSessionViewModel session = view.DataContext as ImageViewerSessionViewModel
                ?? throw new InvalidOperationException("The viewer session must be bound.");
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(ClipboardNavigationTestTimeoutSeconds));
            Key[] navigationKeys = [Key.Left, Key.Right, Key.A, Key.D];
            RawInputModifiers[] modifiers = [RawInputModifiers.None, RawInputModifiers.Control, RawInputModifiers.Shift];

            try
            {
                window.Show();
                await WaitForImageSourceAsync(view, source => source.PixelSize == new PixelSize(SourceImageWidth, SourceImageHeight), timeout.Token);
                await Task.Delay(250, timeout.Token);
                double fittedWidth = view.Image.Width;
                double centeredLeft = Canvas.GetLeft(view.Image);
                double centeredTop = Canvas.GetTop(view.Image);
                Button zoomIn = view.FindControl<Button>("ZoomInButton")
                    ?? throw new InvalidOperationException("The zoom button must exist.");
                zoomIn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await Task.Delay(250, timeout.Token);
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                view.Image.Width.Should().BeGreaterThan(fittedWidth);
                Point panStart = new(window.Bounds.Width / 2d, window.Bounds.Height / 2d);
                window.MouseDown(panStart, MouseButton.Left, RawInputModifiers.None);
                window.MouseMove(new Point(panStart.X + 80d, panStart.Y + 40d), RawInputModifiers.LeftMouseButton);
                window.MouseUp(new Point(panStart.X + 80d, panStart.Y + 40d), MouseButton.Left, RawInputModifiers.None);

                foreach (Key key in navigationKeys)
                {
                    foreach (RawInputModifiers modifier in modifiers)
                    {
                        window.KeyPress(Key.V, RawInputModifiers.Control, PhysicalKey.V, null);
                        await WaitForImageSourceAsync(view, _ => session.IsClipboardImageActive, timeout.Token);
                        Bitmap firstPaste = view.Image.Source as Bitmap
                            ?? throw new InvalidOperationException("The pasted bitmap must be displayed.");
                        window.KeyPress(Key.V, RawInputModifiers.Control | RawInputModifiers.Shift, PhysicalKey.V, null);
                        await WaitForImageSourceAsync(view, source => !ReferenceEquals(source, firstPaste), timeout.Token);
                        Bitmap secondPaste = view.Image.Source as Bitmap
                            ?? throw new InvalidOperationException("The second pasted bitmap must be displayed.");
                        window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
                        window.KeyPress(key, modifier, PhysicalKey.None, null);
                        await WaitForImageSourceAsync(view, source => !ReferenceEquals(source, secondPaste), timeout.Token);

                        session.SelectedItem.Should().BeSameAs(original);
                        session.IsClipboardImageActive.Should().BeFalse();
                        session.IsMainImageModeActive.Should().BeTrue();
                        await Task.Delay(250, timeout.Token);
                        view.Image.Width.Should().BeApproximately(fittedWidth, 0.001d);
                        Canvas.GetLeft(view.Image).Should().BeApproximately(centeredLeft, 0.001d);
                        Canvas.GetTop(view.Image).Should().BeApproximately(centeredTop, 0.001d);
                    }
                }

                foreach (Border navigationArea in new Border[] { view.LeftNavigationArea, view.RightNavigationArea })
                {
                    await window.PasteFromClipboardAsync(timeout.Token);
                    Bitmap pasted = view.Image.Source as Bitmap
                        ?? throw new InvalidOperationException("The pasted bitmap must be displayed.");
                    Point click = view.ViewerArea.TranslatePoint(
                        new Point(ReferenceEquals(navigationArea, view.LeftNavigationArea)
                            ? 1d : view.ViewerArea.Bounds.Width - 1d, view.ViewerArea.Bounds.Height / 2d), window)
                        ?? throw new InvalidOperationException("The navigation area must be arranged.");
                    window.MouseMove(new Point(window.Bounds.Width / 2d, window.Bounds.Height / 2d), RawInputModifiers.None);
                    window.MouseMove(click, RawInputModifiers.None);
                    await Task.Delay(250, timeout.Token);
                    window.UpdateLayout();
                    navigationArea.IsHitTestVisible.Should().BeTrue();
                    window.MouseDown(click, MouseButton.Left, RawInputModifiers.None);
                    window.MouseUp(click, MouseButton.Left, RawInputModifiers.None);
                    await WaitForImageSourceAsync(view, source => !ReferenceEquals(source, pasted), timeout.Token);

                    session.SelectedItem.Should().BeSameAs(original);
                    session.IsClipboardImageActive.Should().BeFalse();
                }
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Theory]
    [InlineData(Key.F8, PhysicalKey.F8, KeyModifiers.None, RawInputModifiers.None)]
    [InlineData(Key.V, PhysicalKey.V, KeyModifiers.Control | KeyModifiers.Alt, RawInputModifiers.Control | RawInputModifiers.Alt)]
    [InlineData(Key.F, PhysicalKey.F, KeyModifiers.Control | KeyModifiers.Alt, RawInputModifiers.Control | RawInputModifiers.Alt)]
    [InlineData(Key.C, PhysicalKey.C, KeyModifiers.Control | KeyModifiers.Alt, RawInputModifiers.Control | RawInputModifiers.Alt)]
    public async Task SetClipboardShortcut_WithFocusedControlRecordingAndReassignment_UsesLatestGestureAndControlV(
        Key key, PhysicalKey physicalKey, KeyModifiers modifiers, RawInputModifiers rawModifiers)
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(typeof(SkiaViewerTestSession), SessionLock, async () =>
        {
            using PicaTemporaryDirectory directory = new();
            string path = await CreateImageAsync(directory.DirectoryPath);
            PicaImageItem original = new(ItemId, path, "original.png");
            PicaViewerRequest request = new(new PicaImageItem[] { original }, ItemId);
            int reads = 0;
            DelegateClipboardImageReader reader = new()
            {
                Read = _ =>
                {
                    reads++;
                    return Task.FromResult<IReadOnlyList<ClipboardImageInput>>(
                        new ClipboardImageInput[] { ClipboardImageInput.FromBitmap(CreateBitmap(SourceImageWidth, SourceImageHeight)) });
                }
            };
            ImageViewerWindow window = CreateWindow(request, new ImageViewerState { IsFastLoadingEnabled = false },
                new RecordingImageChannelBitmapLoader(), clipboardReader: reader);
            ImageViewerView view = window.Content as ImageViewerView
                ?? throw new InvalidOperationException("The viewer must expose its view.");
            ImageViewerSessionViewModel session = view.DataContext as ImageViewerSessionViewModel
                ?? throw new InvalidOperationException("The viewer must expose its session.");
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(ClipboardNavigationTestTimeoutSeconds));

            try
            {
                window.Show();
                await WaitForImageSourceAsync(view, source => source.PixelSize == new PixelSize(SourceImageWidth, SourceImageHeight), timeout.Token);
                window.SetClipboardShortcut(new ConfiguredViewerClipboardShortcut(key, modifiers));
                view.ViewerArea.Focusable = true;
                view.ViewerArea.AddHandler(InputElement.KeyDownEvent, (_, args) => args.Handled = true,
                    RoutingStrategies.Tunnel);
                view.ViewerArea.Focus().Should().BeTrue();
                window.SetValue(Pica.Viewer.Controls.ViewerSettingRecording.IsActiveProperty, true);
                window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
                window.KeyPress(key, rawModifiers, physicalKey, null);
                window.KeyPress(Key.V, RawInputModifiers.Control, PhysicalKey.V, null);
                await window.PasteFromClipboardAsync(timeout.Token);
                reads.Should().Be(0);
                session.IsMainImageModeActive.Should().BeTrue();
                window.ClearValue(Pica.Viewer.Controls.ViewerSettingRecording.IsActiveProperty);
                window.KeyPress(key, rawModifiers, physicalKey, null);
                await WaitForImageSourceAsync(view, _ => session.IsClipboardImageActive, timeout.Token);
                Bitmap firstPaste = view.Image.Source as Bitmap
                    ?? throw new InvalidOperationException("The pasted bitmap must be shown.");
                window.SetClipboardShortcut(new ConfiguredViewerClipboardShortcut(Key.F9));
                window.KeyPress(key, rawModifiers, physicalKey, null);
                reads.Should().Be(1);
                window.KeyPress(Key.F9, RawInputModifiers.None, PhysicalKey.F9, null);
                await WaitForImageSourceAsync(view, source => !ReferenceEquals(source, firstPaste), timeout.Token);
                Bitmap secondPaste = view.Image.Source as Bitmap
                    ?? throw new InvalidOperationException("The next pasted bitmap must be shown.");
                window.KeyPress(Key.V, RawInputModifiers.Control, PhysicalKey.V, null);
                await WaitForImageSourceAsync(view, source => !ReferenceEquals(source, secondPaste), timeout.Token);

                reads.Should().Be(3);
                session.IsClipboardImageActive.Should().BeTrue();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task ModifiedNavigationKeys_WithDifferentStillImageSizes_NavigateAndResetImageLayout()
    {
        await DispatchAsync(async () =>
        {
            using PicaTemporaryDirectory temporaryDirectory = new();
            string imagePath = await CreateImageAsync(
                temporaryDirectory.DirectoryPath);
            PicaImageItem item = new(
                ItemId,
                imagePath,
                "image.heif");
            PicaViewerRequest request = new(
                new List<PicaImageItem> { item },
                ItemId);
            ControlledFullResolutionImageLoader fullResolutionLoader = new(
                new List<string> { imagePath });
            ImageViewerState state = new()
            {
                IsFastLoadingEnabled = false,
                PreserveZoomAndPositionOnNavigation = true,
                ResizeBehavior = WindowResizeBehavior.Free
            };
            ImageViewerWindow window = CreateWindow(
                request,
                state,
                new RecordingImageChannelBitmapLoader(),
                new ImagePreviewLoader(
                    new ImageFormatRegistry(),
                    NullLogger<ImagePreviewLoader>.Instance),
                fullResolutionLoader);
            ImageViewerView view = window.Content as ImageViewerView
                ?? throw new InvalidOperationException(
                    "The viewer content must be created.");
            using CancellationTokenSource timeout = new(
                TimeSpan.FromSeconds(TestTimeoutSeconds));
            Bitmap firstBitmap = CreateBitmap(1024, 536);
            Bitmap secondBitmap = CreateBitmap(640, 480);
            DecodedImage image = new(
                [
                    new DecodedImageFrame(firstBitmap, TimeSpan.Zero),
                    new DecodedImageFrame(secondBitmap, TimeSpan.Zero)
                ],
                ImageFramePresentationModes.ManualNavigation,
                0);

            try
            {
                window.Show();
                await fullResolutionLoader.WaitUntilStartedAsync(
                    imagePath,
                    timeout.Token);
                fullResolutionLoader.Complete(imagePath, image);
                await WaitForImageSourceAsync(
                    view,
                    firstBitmap,
                    timeout.Token);

                window.KeyPress(
                    Key.OemPeriod,
                    RawInputModifiers.None,
                    PhysicalKey.Period,
                    null);
                await WaitForImageSourceAsync(
                    view,
                    firstBitmap,
                    timeout.Token);

                window.KeyPress(
                    Key.Right,
                    RawInputModifiers.Shift,
                    PhysicalKey.ArrowRight,
                    null);
                await WaitForImageSourceAsync(
                    view,
                    secondBitmap,
                    timeout.Token);

                Size viewportSize = view.ViewerArea.Bounds.Size;
                double renderScaling = window.RenderScaling;
                double expectedScale =
                    ImageWindowGeometry.CalculateFittedScale(
                        secondBitmap.PixelSize,
                        new Size(
                            viewportSize.Width * renderScaling,
                            viewportSize.Height * renderScaling));
                double expectedWidth =
                    secondBitmap.PixelSize.Width
                    * expectedScale
                    / renderScaling;
                double expectedHeight =
                    secondBitmap.PixelSize.Height
                    * expectedScale
                    / renderScaling;
                view.Image.Width.Should().BeApproximately(
                    expectedWidth,
                    0.001d);
                view.Image.Height.Should().BeApproximately(
                    expectedHeight,
                    0.001d);
                Canvas.GetLeft(view.Image).Should().BeApproximately(
                    (viewportSize.Width - expectedWidth) / 2d,
                    0.001d);
                Canvas.GetTop(view.Image).Should().BeApproximately(
                    (viewportSize.Height - expectedHeight) / 2d,
                    0.001d);
                view.CheckerboardBackground.Width.Should().Be(
                    view.Image.Width);
                view.CheckerboardBackground.Height.Should().Be(
                    view.Image.Height);

                window.KeyPress(
                    Key.Left,
                    RawInputModifiers.Shift,
                    PhysicalKey.ArrowLeft,
                    null);
                await WaitForImageSourceAsync(
                    view,
                    firstBitmap,
                    timeout.Token);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task FramePunctuationKeys_WithAnimation_NavigateFrames()
    {
        await DispatchAsync(async () =>
        {
            using PicaTemporaryDirectory temporaryDirectory = new();
            string imagePath = await CreateImageAsync(
                temporaryDirectory.DirectoryPath);
            PicaImageItem item = new(
                ItemId,
                imagePath,
                "image.gif");
            PicaViewerRequest request = new(
                new List<PicaImageItem> { item },
                ItemId);
            ControlledFullResolutionImageLoader fullResolutionLoader = new(
                new List<string> { imagePath });
            ImageViewerState state = new()
            {
                IsFastLoadingEnabled = false,
                ResizeBehavior = WindowResizeBehavior.Free
            };
            ImageViewerWindow window = CreateWindow(
                request,
                state,
                new RecordingImageChannelBitmapLoader(),
                new ImagePreviewLoader(
                    new ImageFormatRegistry(),
                    NullLogger<ImagePreviewLoader>.Instance),
                fullResolutionLoader);
            ImageViewerView view = window.Content as ImageViewerView
                ?? throw new InvalidOperationException(
                    "The viewer content must be created.");
            using CancellationTokenSource timeout = new(
                TimeSpan.FromSeconds(TestTimeoutSeconds));
            Bitmap firstBitmap = CreateBitmap(640, 360);
            Bitmap secondBitmap = CreateBitmap(640, 360);
            DecodedImage animation = new(
                [
                    new DecodedImageFrame(
                        firstBitmap,
                        TimeSpan.FromHours(1d)),
                    new DecodedImageFrame(
                        secondBitmap,
                        TimeSpan.FromHours(1d))
                ],
                ImageFramePresentationModes.AutomaticPlayback,
                0);

            try
            {
                window.Show();
                await fullResolutionLoader.WaitUntilStartedAsync(
                    imagePath,
                    timeout.Token);
                fullResolutionLoader.Complete(imagePath, animation);
                await WaitForImageSourceAsync(
                    view,
                    firstBitmap,
                    timeout.Token);

                window.KeyPress(
                    Key.OemPeriod,
                    RawInputModifiers.None,
                    PhysicalKey.Period,
                    null);
                await WaitForImageSourceAsync(
                    view,
                    secondBitmap,
                    timeout.Token);

                window.KeyPress(
                    Key.None,
                    RawInputModifiers.None,
                    PhysicalKey.Comma,
                    null);
                await WaitForImageSourceAsync(
                    view,
                    firstBitmap,
                    timeout.Token);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task ModifiedNavigationKeys_WithMixedContent_CycleImagesAndAnimations()
    {
        await DispatchAsync(async () =>
        {
            using PicaTemporaryDirectory temporaryDirectory = new();
            string imagePath = await CreateImageAsync(
                temporaryDirectory.DirectoryPath);
            PicaImageItem item = new(
                ItemId,
                imagePath,
                "image.avif");
            PicaViewerRequest request = new(
                new List<PicaImageItem> { item },
                ItemId);
            ControlledFullResolutionImageLoader fullResolutionLoader = new(
                new List<string> { imagePath });
            ImageViewerState state = new()
            {
                IsFastLoadingEnabled = false,
                ResizeBehavior = WindowResizeBehavior.Free
            };
            ImageViewerWindow window = CreateWindow(
                request,
                state,
                new RecordingImageChannelBitmapLoader(),
                new ImagePreviewLoader(
                    new ImageFormatRegistry(),
                    NullLogger<ImagePreviewLoader>.Instance),
                fullResolutionLoader);
            ImageViewerView view = window.Content as ImageViewerView
                ?? throw new InvalidOperationException(
                    "The viewer content must be created.");
            using CancellationTokenSource timeout = new(
                TimeSpan.FromSeconds(TestTimeoutSeconds));
            Bitmap firstStillBitmap = CreateBitmap(640, 360);
            Bitmap secondStillBitmap = CreateBitmap(480, 480);
            Bitmap animationBitmap = CreateBitmap(360, 640);
            DecodedImage stillImages = new(
                [
                    new DecodedImageFrame(
                        firstStillBitmap,
                        TimeSpan.Zero),
                    new DecodedImageFrame(
                        secondStillBitmap,
                        TimeSpan.Zero)
                ],
                ImageFramePresentationModes.ManualNavigation,
                0);
            DecodedImage animation = new(
                [
                    new DecodedImageFrame(
                        animationBitmap,
                        TimeSpan.FromMilliseconds(100d))
                ],
                ImageFramePresentationModes.AutomaticPlayback,
                0);
            DecodedImageContent content = new(
                [
                    new DecodedImageContentGroup(
                        new ImageContentGroupDefinition(
                            ImageContentGroupKind.StillImages,
                            2),
                        stillImages),
                    new DecodedImageContentGroup(
                        new ImageContentGroupDefinition(
                            ImageContentGroupKind.Animation,
                            1),
                        animation)
                ],
                0);

            try
            {
                window.Show();
                await fullResolutionLoader.WaitUntilStartedAsync(
                    imagePath,
                    timeout.Token);
                fullResolutionLoader.Complete(imagePath, content);
                await WaitForImageSourceAsync(
                    view,
                    firstStillBitmap,
                    timeout.Token);

                window.KeyPress(
                    Key.D,
                    RawInputModifiers.Shift,
                    PhysicalKey.D,
                    null);
                await WaitForImageSourceAsync(
                    view,
                    secondStillBitmap,
                    timeout.Token);

                window.KeyPress(
                    Key.Right,
                    RawInputModifiers.Control,
                    PhysicalKey.ArrowRight,
                    null);
                await WaitForImageSourceAsync(
                    view,
                    animationBitmap,
                    timeout.Token);

                window.KeyPress(
                    Key.Left,
                    RawInputModifiers.Alt,
                    PhysicalKey.ArrowLeft,
                    null);
                await WaitForImageSourceAsync(
                    view,
                    secondStillBitmap,
                    timeout.Token);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task Constructor_WithViewerContent_HostsCompleteView()
    {
        await DispatchAsync(() =>
        {
            ImageViewerWindow window = CreateWindow();

            try
            {
                window.Show();

                window.Content.Should().BeOfType<ImageViewerView>();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task CreateAsync_WithoutActionsOrDispatcher_CreatesWindow()
    {
        await DispatchAsync(async () =>
        {
            IImageViewerWindowFactory factory = CreateWindowFactory(
                new ImageViewerState(),
                new RecordingImageChannelBitmapLoader());
            PicaViewerRequest request = CreateEmptyRequest();

            ImageViewerWindow window = await factory.CreateAsync(
                request,
                CancellationToken.None);

            try
            {
                window.Show();

                window.Content.Should().BeOfType<ImageViewerView>();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task CreateAsync_WithActionsAndWithoutDispatcher_ThrowsArgumentException()
    {
        await DispatchAsync(async () =>
        {
            IImageViewerWindowFactory factory = CreateWindowFactory(
                new ImageViewerState(),
                new RecordingImageChannelBitmapLoader());
            PicaActionDefinition action = new(
                "test.action",
                "Test action",
                "M0,0 L1,1",
                0d,
                PicaActionTargets.CurrentImage,
                0);
            PicaViewerRequest request = new(
                new List<PicaImageItem>(),
                Guid.Empty,
                new List<PicaActionDefinition> { action });

            Func<Task> act = () => factory.CreateAsync(
                request,
                CancellationToken.None);

            await act.Should()
                .ThrowAsync<ArgumentException>()
                .WithParameterName(nameof(request));
        });
    }

    [Fact]
    public async Task Constructor_WithImageInformationSettings_PlacesResolutionAfterModificationDate()
    {
        await DispatchAsync(() =>
        {
            ImageViewerWindow window = CreateWindow();

            try
            {
                window.Show();
                List<string> imageInformationSettings = window
                    .GetLogicalDescendants()
                    .OfType<CheckBox>()
                    .Select(checkBox => checkBox.Content)
                    .OfType<string>()
                    .Where(content => content.StartsWith(
                        "Показывать",
                        StringComparison.Ordinal))
                    .ToList();

                imageInformationSettings.Should().Equal(
                    "Показывать название",
                    "Показывать формат",
                    "Показывать дату изменения",
                    "Показывать разрешение");
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task ImageSourceWorkflow_WithFastLoading_ReplacesPreviewWithFullResolutionAndSelectedChannel()
    {
        await DispatchAsync(async () =>
        {
            using PicaTemporaryDirectory temporaryDirectory = new();
            string imagePath = await CreateImageAsync(
                temporaryDirectory.DirectoryPath);
            await using (FileStream sourceStream = File.OpenRead(imagePath))
            using (SKManagedStream managedSource = new(sourceStream))
            using (SKCodec sourceCodec = SKCodec.Create(managedSource)
                ?? throw new InvalidDataException(
                    "The test image could not be decoded."))
            {
                sourceCodec.Info.Width.Should().Be(SourceImageWidth);
                sourceCodec.Info.Height.Should().Be(SourceImageHeight);
            }
            PicaImageItem item = new(
                ItemId,
                imagePath,
                "image.png");
            PicaViewerRequest request = new(
                new List<PicaImageItem> { item },
                ItemId);
            ImageViewerState state = new()
            {
                IsFastLoadingEnabled = true
            };
            ImageViewerWindow window = CreateWindow(
                request,
                state,
                new ImageChannelBitmapLoader(
                    new ImageFormatRegistry()));
            ImageViewerView view = window.Content as ImageViewerView
                ?? throw new InvalidOperationException(
                    "The viewer content must be created.");
            TaskCompletionSource<Bitmap> previewSource = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<Bitmap> fullResolutionSource = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<Bitmap> channelSource = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            Bitmap? capturedPreviewSource = null;
            Bitmap? capturedFullResolutionSource = null;
            List<PixelSize> observedSourceSizes = [];
            view.Image.PropertyChanged += (_, e) =>
            {
                if ((e.Property != Image.SourceProperty)
                    || (view.Image.Source is not Bitmap source))
                {
                    return;
                }

                observedSourceSizes.Add(source.PixelSize);

                if ((capturedPreviewSource is null)
                    && (source.PixelSize.Width
                        == ImagePreviewLoader.PreviewDecodeWidth))
                {
                    capturedPreviewSource = source;
                    previewSource.TrySetResult(source);
                    return;
                }

                if ((capturedPreviewSource is null)
                    || object.ReferenceEquals(
                        capturedPreviewSource,
                        source))
                {
                    return;
                }

                if (capturedFullResolutionSource is null)
                {
                    capturedFullResolutionSource = source;
                    fullResolutionSource.TrySetResult(source);
                    return;
                }

                if (!object.ReferenceEquals(
                    capturedFullResolutionSource,
                    source))
                {
                    channelSource.TrySetResult(source);
                }
            };

            try
            {
                window.Show();
                Bitmap preview = await previewSource.Task.WaitAsync(
                    TimeSpan.FromSeconds(TestTimeoutSeconds));
                Bitmap fullResolution =
                    await fullResolutionSource.Task.WaitAsync(
                        TimeSpan.FromSeconds(TestTimeoutSeconds));

                window.KeyPress(
                    Key.Tab,
                    RawInputModifiers.None,
                    PhysicalKey.Tab,
                    null);
                Bitmap channel = await channelSource.Task.WaitAsync(
                    TimeSpan.FromSeconds(TestTimeoutSeconds));

                preview.Should().NotBeSameAs(fullResolution);
                fullResolution.Should().NotBeSameAs(channel);
                view.Image.Source.Should().BeSameAs(channel);
                channel.PixelSize.Should().Be(fullResolution.PixelSize);
            }
            catch (TimeoutException ex)
            {
                string currentSourceSize = view.Image.Source is Bitmap source
                    ? source.PixelSize.ToString()
                    : "none";
                throw new InvalidOperationException(
                    $"The image source workflow stopped at {currentSourceSize}. "
                    + $"Preview: {previewSource.Task.IsCompleted}; "
                    + $"full resolution: {fullResolutionSource.Task.IsCompleted}; "
                    + $"channel: {channelSource.Task.IsCompleted}; "
                    + $"observed sources: {string.Join(", ", observedSourceSizes)}.",
                    ex);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task OnClosed_WithBlockedImageLoad_CompletesAfterBitmapDisposalAndVisualDetachment()
    {
        await DispatchAsync(async () =>
        {
            using PicaTemporaryDirectory temporaryDirectory = new();
            string imagePath = await CreateImageAsync(
                temporaryDirectory.DirectoryPath);
            PicaImageItem item = new(
                ItemId,
                imagePath,
                "image.png");
            PicaViewerRequest request = new(
                new List<PicaImageItem> { item },
                ItemId);
            ControlledFullResolutionImageLoader fullResolutionLoader = new(
                new List<string> { imagePath });
            ImageViewerWindow window = CreateWindow(
                request,
                new ImageViewerState(),
                new RecordingImageChannelBitmapLoader(),
                new ImagePreviewLoader(
                    new ImageFormatRegistry(),
                    NullLogger<ImagePreviewLoader>.Instance),
                fullResolutionLoader);
            ImageViewerView view = window.Content as ImageViewerView
                ?? throw new InvalidOperationException(
                    "The viewer content must be created.");
            TaskCompletionSource closed = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            window.Closed += (_, _) => closed.TrySetResult();
            using CancellationTokenSource timeout = new(
                TimeSpan.FromSeconds(TestTimeoutSeconds));
            window.Show();
            await fullResolutionLoader.WaitUntilStartedAsync(
                imagePath,
                timeout.Token);

            window.Close();
            await closed.Task.WaitAsync(timeout.Token);
            Task closeCleanupCompletion =
                window.CloseCleanupCompletion;

            fullResolutionLoader
                .GetCancellationToken(imagePath)
                .IsCancellationRequested.Should().BeTrue();
            closeCleanupCompletion.IsCompleted.Should().BeFalse();
            TrackingBitmap bitmap = new(imagePath);
            fullResolutionLoader.Complete(imagePath, bitmap);
            await closeCleanupCompletion.WaitAsync(timeout.Token);

            bitmap.IsDisposed.Should().BeTrue();
            view.Image.Source.Should().BeNull();
            view.DataContext.Should().BeNull();
            window.Content.Should().BeNull();
            view.WindowResizeOverlay.Parent.Should().BeNull();
            view.WindowResizeOverlay.GetVisualParent().Should().BeNull();
            window.LogoContent.Should().BeNull();
            window.RightWindowTitleBarControls.Should().BeEmpty();
            window.Hosts.Should().BeEmpty();
            window.Icon.Should().BeNull();
        });
    }

    [Fact]
    public async Task OnClosed_AfterCleanup_ReleasesWindowReference()
    {
        WeakReference? windowReference = null;
        await DispatchAsync(async () =>
        {
            windowReference =
                await CreateClosedWindowWeakReferenceAsync();
        });

        CollectGarbage();

        windowReference.Should().NotBeNull();
        windowReference.IsAlive.Should().BeFalse();
    }

    private static void AssertResizeEdge(ImageViewerWindow window, Point position, WindowSizingEdges edges)
    {
        Border border = window.InputHitTest(position).Should().BeAssignableTo<Border>().Subject;
        border.Tag.Should().Be(edges);
    }

    private static PicaViewerRequest CreateEmptyRequest()
    {
        return new PicaViewerRequest(
            new List<PicaImageItem>(),
            Guid.Empty);
    }

    private static async Task<ImageViewerWindow> CreateLoadedWindowAsync(
        string directoryPath,
        ImageViewerState state,
        CancellationToken ct)
    {
        string imagePath = await CreateImageAsync(directoryPath);
        PicaViewerRequest request = new(new List<PicaImageItem>
        {
            new(ItemId, imagePath, "image.png")
        }, ItemId);
        ControlledFullResolutionImageLoader loader = new(new List<string> { imagePath });
        ImageViewerWindow window = CreateWindow(request, state, new RecordingImageChannelBitmapLoader(),
            new ImagePreviewLoader(new ImageFormatRegistry(), NullLogger<ImagePreviewLoader>.Instance), loader);
        ImageViewerView view = window.Content as ImageViewerView
            ?? throw new InvalidOperationException("The viewer content must be created.");

        try
        {
            window.Show();
            await loader.WaitUntilStartedAsync(imagePath, ct);
            Bitmap bitmap = CreateBitmap(SourceImageWidth, SourceImageHeight);
            loader.Complete(imagePath, bitmap);
            await WaitForImageSourceAsync(view, bitmap, ct);
            window.UpdateLayout();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.Background);

            return window;
        }
        catch (Exception)
        {
            window.Close();
            throw;
        }
    }

    private static ImageViewerState CreateBackgroundClickState()
    {
        ImageViewerState state = CreateWindowedState();
        state.ExpandOnDoubleClick = true;
        state.IsFastLoadingEnabled = false;
        state.IsPanningInertiaEnabled = false;
        state.ResizeBehavior = WindowResizeBehavior.Free;
        state.WindowWidth = 800d;
        state.WindowHeight = 800d;

        return state;
    }

    private static Point GetViewerClickPosition(
        ImageViewerWindow window,
        bool clickBackground)
    {
        ImageViewerView view = window.Content as ImageViewerView
            ?? throw new InvalidOperationException("The viewer content must be created.");
        double imageTop = Canvas.GetTop(view.Image);
        double titleBarBottom = window.IsTitleBarOverlayEnabled
            && (window.CurrentWindowMode == ViewerWindowMode.Windowed)
            ? window.GetVisualDescendants().OfType<Control>()
                .Single(control => control.Name == "PART_TitleBar").Bounds.Bottom
            : 0d;
        imageTop.Should().BeGreaterThan(titleBarBottom);
        double positionY = clickBackground
            ? (titleBarBottom + imageTop) / 2d
            : imageTop + (view.Image.Height / 2d);

        return view.ViewerArea.TranslatePoint(
            new Point(view.ViewerArea.Bounds.Width / 2d, positionY), window)
            ?? throw new InvalidOperationException("The viewer area must be attached to the window.");
    }

    private static async Task WaitForOpacityAsync(Control control, double expectedOpacity, CancellationToken ct)
    {
        while (control.Opacity != expectedOpacity)
        {
            await Task.Delay(20, ct);
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
    }

    private static CheckBox GetTitleBarAutoHideSetting(
        ImageViewerView view,
        out ViewerSettingsContentControl settingsContent)
    {
        settingsContent = view.SettingsPanel.GetLogicalDescendants()
            .OfType<ViewerSettingsContentControl>().Single();

        return settingsContent.GetLogicalDescendants().OfType<CheckBox>().Single(checkBox =>
            checkBox.Content is TextBlock { Text: "Автоматически скрывать заголовок окна" });
    }

    private static ImageViewerState CreateWindowedState()
    {
        return new ImageViewerState
        {
            ExpandOnDoubleClick = false,
            IsWindowed = true,
            RememberWindowPlacement = true
        };
    }

    private static void DoubleClick(
        ImageViewerWindow window,
        Point position,
        MouseButton button = MouseButton.Left,
        RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.MouseMove(position, modifiers);
        window.MouseDown(
            position,
            button,
            modifiers);
        window.MouseUp(
            position,
            button,
            modifiers);
        window.MouseDown(
            position,
            button,
            modifiers);
        window.MouseUp(
            position,
            button,
            modifiers);
    }

    private static ImageViewerWindow CreateWindow()
    {
        ImageViewerState state = new();

        return CreateWindow(
            CreateEmptyRequest(),
            state,
            new RecordingImageChannelBitmapLoader());
    }

    private static ImageViewerWindow CreateWindow(
        PicaViewerRequest request,
        ImageViewerState state,
        IImageChannelBitmapLoader channelBitmapLoader,
        IPlatformFileActions? platformFileActions = null,
        RecordingViewerActionDispatcher? actionDispatcher = null,
        IClipboardImageReader? clipboardReader = null)
    {
        ImageFormatRegistry formatRegistry = new();

        return CreateWindow(
            request,
            state,
            channelBitmapLoader,
            new ImagePreviewLoader(
                formatRegistry,
                NullLogger<ImagePreviewLoader>.Instance),
            new FullResolutionImageLoader(
                formatRegistry,
                MultiFrameImageDecoderTestFactory.Create()),
            platformFileActions,
            actionDispatcher,
            clipboardReader);
    }

    private static ImageViewerWindow CreateWindow(
        PicaViewerRequest request,
        ImageViewerState state,
        IImageChannelBitmapLoader channelBitmapLoader,
        IImagePreviewLoader previewLoader,
        IFullResolutionImageLoader fullResolutionLoader,
        IPlatformFileActions? platformFileActions = null,
        RecordingViewerActionDispatcher? actionDispatcher = null,
        IClipboardImageReader? clipboardReader = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(channelBitmapLoader);
        ArgumentNullException.ThrowIfNull(previewLoader);
        ArgumentNullException.ThrowIfNull(fullResolutionLoader);
        RecordingImageViewerStateService stateService = new(state);
        AvaloniaViewerUiDispatcher uiDispatcher = new();
        ImageViewerWindowComposer composer = CreateWindowComposer(
            stateService,
            uiDispatcher,
            channelBitmapLoader,
            previewLoader,
            fullResolutionLoader,
            platformFileActions,
            clipboardReader);

        return composer.Create(
            request,
            actionDispatcher ?? new RecordingViewerActionDispatcher(),
            state,
            Array.Empty<ViewerSettingContribution>());
    }

    private static IImageViewerWindowFactory CreateWindowFactory(
        ImageViewerState state,
        IImageChannelBitmapLoader channelBitmapLoader)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(channelBitmapLoader);
        RecordingImageViewerStateService stateService = new(state);
        AvaloniaViewerUiDispatcher uiDispatcher = new();
        ImageViewerWindowComposer composer = CreateWindowComposer(
            stateService,
            uiDispatcher,
            channelBitmapLoader);

        return new ImageViewerWindowFactory(
            stateService,
            uiDispatcher,
            composer,
            Array.Empty<IViewerSettingContributionProvider>());
    }

    private static ImageViewerWindowComposer CreateWindowComposer(
        IImageViewerStateService stateService,
        IViewerUiDispatcher uiDispatcher,
        IImageChannelBitmapLoader channelBitmapLoader,
        IImagePreviewLoader? previewLoader = null,
        IFullResolutionImageLoader? fullResolutionLoader = null,
        IPlatformFileActions? platformFileActions = null,
        IClipboardImageReader? clipboardReader = null)
    {
        ArgumentNullException.ThrowIfNull(stateService);
        ArgumentNullException.ThrowIfNull(uiDispatcher);
        ArgumentNullException.ThrowIfNull(channelBitmapLoader);
        ImageFormatRegistry formatRegistry = new();
        previewLoader ??= new ImagePreviewLoader(
            formatRegistry,
            NullLogger<ImagePreviewLoader>.Instance);
        fullResolutionLoader ??=
            new FullResolutionImageLoader(
                formatRegistry,
                MultiFrameImageDecoderTestFactory.Create());
        ViewModelErrorHandler errorHandler = new(
            NullLogger<ViewModelErrorHandler>.Instance);
        ImageViewerPresentationFactory presentationFactory = new(
            previewLoader,
            fullResolutionLoader,
            channelBitmapLoader,
            new ImageAnimationDelayScheduler(),
            uiDispatcher,
            NullLogger<ImagePresentationController>.Instance,
            NullLogger<ImageAnimationPlaybackController>.Instance,
            NullLogger<ImageLoadCoordinator>.Instance,
            NullLogger<ImagePreviewPrefetcher>.Instance);
        ImageViewerSettingsFactory settingsFactory = new(
            stateService,
            new ImageFileMetadataProvider(
                NullLogger<ImageFileMetadataProvider>.Instance),
            errorHandler);
        ClipboardImagePreparer clipboardImagePreparer = new();
        ClipboardFlushCoordinator flushCoordinator = new();
        ViewerClipboardFactory clipboardFactory = new(
            clipboardImagePreparer,
            flushCoordinator,
            NullLogger<AvaloniaClipboardDataWriter>.Instance,
            clipboardReader ?? new DelegateClipboardImageReader());
        ImageViewerInteractionFactory interactionFactory = new(
            clipboardFactory,
            formatRegistry,
            uiDispatcher,
            new PngImageEncoder(),
            clipboardImagePreparer,
            platformFileActions ?? new NullPlatformFileActions(),
            errorHandler,
            NullLogger<ImageViewerActionsViewModel>.Instance,
            NullLogger<ImageViewerOpenWithViewModel>.Instance,
            NullLogger<TemporaryImageFileStore>.Instance,
            new FullResolutionImageLoader(formatRegistry, MultiFrameImageDecoderTestFactory.Create()),
            new ClipboardImageFormatCatalog(formatRegistry),
            NullLogger<ViewerClipboardPasteService>.Instance);
        ImageViewerWindowComposer composer = new(
            presentationFactory,
            settingsFactory,
            interactionFactory,
            NullLogger<ImageViewerWindow>.Instance,
            NullLogger<ImageViewerWindowLifetime>.Instance);

        return composer;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<WeakReference>
        CreateClosedWindowWeakReferenceAsync()
    {
        ImageViewerWindow window = CreateWindow();
        TaskCompletionSource closed = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        window.Closed += (_, _) => closed.TrySetResult();
        window.Show();

        window.Close();
        await closed.Task.WaitAsync(
            TimeSpan.FromSeconds(TestTimeoutSeconds));
        await window.CloseCleanupCompletion.WaitAsync(
            TimeSpan.FromSeconds(TestTimeoutSeconds));

        return new WeakReference(window);
    }

    private static void CollectGarbage()
    {
        GC.Collect(
            GC.MaxGeneration,
            GCCollectionMode.Forced,
            true,
            true);
        GC.WaitForPendingFinalizers();
        GC.Collect(
            GC.MaxGeneration,
            GCCollectionMode.Forced,
            true,
            true);
    }

    private static async Task<string> CreateImageAsync(
        string directoryPath)
    {
        string imagePath = Path.Combine(
            directoryPath,
            "image.png");
        SKImageInfo imageInfo = new(
            SourceImageWidth,
            SourceImageHeight,
            SKColorType.Bgra8888,
            SKAlphaType.Unpremul);
        using SKBitmap bitmap = new(imageInfo);

        for (int y = 0; y < SourceImageHeight; y++)
        {
            for (int x = 0; x < SourceImageWidth; x++)
            {
                bitmap.SetPixel(
                    x,
                    y,
                    new SKColor(
                        (byte)((x + y) % 239),
                        (byte)(y % 241),
                        (byte)(x % 251),
                        byte.MaxValue));
            }
        }

        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData content = image.Encode(
            SKEncodedImageFormat.Png,
            100);
        await File.WriteAllBytesAsync(
            imagePath,
            content.ToArray());

        return imagePath;
    }

    private static Bitmap CreateBitmap(
        int width,
        int height)
    {
        return new WriteableBitmap(
            new PixelSize(width, height),
            new Vector(96d, 96d),
            PixelFormat.Bgra8888,
            AlphaFormat.Unpremul);
    }

    private static async Task WaitForImageSourceAsync(
        ImageViewerView view,
        Bitmap expectedBitmap,
        CancellationToken ct)
    {
        await WaitForImageSourceAsync(
            view,
            source => object.ReferenceEquals(source, expectedBitmap),
            ct);
    }

    private static async Task WaitForImageSourceAsync(
        ImageViewerView view,
        Func<Bitmap, bool> matches,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(matches);

        if (view.Image.Source is Bitmap currentSource
            && matches(currentSource))
        {
            return;
        }

        TaskCompletionSource sourceChanged = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        void OnImagePropertyChanged(
            object? sender,
            AvaloniaPropertyChangedEventArgs e)
        {
            _ = sender;

            if ((e.Property == Image.SourceProperty)
                && (view.Image.Source is Bitmap source)
                && matches(source))
            {
                sourceChanged.TrySetResult();
            }
        }

        view.Image.PropertyChanged += OnImagePropertyChanged;

        try
        {
            await sourceChanged.Task.WaitAsync(ct);
        }
        finally
        {
            view.Image.PropertyChanged -= OnImagePropertyChanged;
        }
    }

    private static async Task DispatchAsync(Action action)
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(
            typeof(ImageViewerWindowTests),
            SessionLock,
            action).ConfigureAwait(false);
    }

    private static async Task DispatchAsync(Func<Task> action)
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(
            typeof(ImageViewerWindowTests),
            SessionLock,
            action).ConfigureAwait(false);
    }
}
