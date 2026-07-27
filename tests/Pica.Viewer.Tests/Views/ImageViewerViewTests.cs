using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using FluentAssertions;
using Xunit;

using Pica.Tests.Common;

using Pica.Protocol;
using Pica.Viewer.Controls;
using Pica.Viewer.Views;

namespace Pica.Viewer.Tests.Views;

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
    public void Constructor_WithOrderedCurrentImageActions_PlacesLastActionBeforeRevealInFolder()
    {
        Dispatch(() =>
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

            using ImageViewerView view = new(
                false,
                new List<ViewerSettingControl>(),
                actions,
                ViewerWindowMode.FullScreen,
                CreateEvents());

            StackPanel menuItems = view.ContextMenu
                .Child
                .Should()
                .BeOfType<StackPanel>()
                .Subject;
            List<Button> buttons = menuItems.Children.OfType<Button>().ToList();
            int showInGalleryIndex = buttons.FindIndex(button =>
                ReferenceEquals(button.Tag, showInGalleryAction));
            int revealInFolderIndex = buttons.FindIndex(button =>
                string.Equals(
                    GetMenuButtonText(button),
                    "Показать в папке",
                    StringComparison.Ordinal));

            showInGalleryIndex.Should().Be(revealInFolderIndex - 1);
        });
    }

    private static ImageViewerViewEvents CreateEvents()
    {
        return new ImageViewerViewEvents
        {
            ZoomOutClicked = IgnoreRoutedEvent,
            ResetClicked = IgnoreRoutedEvent,
            ZoomInClicked = IgnoreRoutedEvent,
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

    private static string? GetMenuButtonText(Button button)
    {
        if (button.Content is not StackPanel content)
        {
            return null;
        }

        return content.Children.OfType<TextBlock>().SingleOrDefault()?.Text;
    }

    private static void Dispatch(Action action)
    {
        HeadlessTestSessionDispatcher.Dispatch(
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
