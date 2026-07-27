using Avalonia.Input;
using Avalonia.Interactivity;

namespace Pica.Viewer.Views;

internal sealed class ImageViewerViewEvents
{
    internal required EventHandler<RoutedEventArgs> ZoomOutClicked { get; init; }
    internal required EventHandler<RoutedEventArgs> ResetClicked { get; init; }
    internal required EventHandler<RoutedEventArgs> ZoomInClicked { get; init; }
    internal required EventHandler<RoutedEventArgs> CloseClicked { get; init; }
    internal required EventHandler<RoutedEventArgs> WindowModeClicked { get; init; }
    internal required EventHandler<RoutedEventArgs> SettingsClicked { get; init; }
    internal required EventHandler<RoutedEventArgs> ContextCopyClicked { get; init; }
    internal required EventHandler<RoutedEventArgs> ContextExternalActionClicked { get; init; }
    internal required EventHandler<RoutedEventArgs> ContextSaveAsClicked { get; init; }
    internal required EventHandler<RoutedEventArgs> ContextRevealInFolderClicked { get; init; }
    internal required EventHandler<RoutedEventArgs> ContextOpenWithClicked { get; init; }
    internal required EventHandler<RoutedEventArgs> ContextSelectAreaClicked { get; init; }
    internal required EventHandler<RoutedEventArgs> SelectionCopyClicked { get; init; }
    internal required EventHandler<RoutedEventArgs> SelectionExternalActionClicked { get; init; }
    internal required EventHandler<RoutedEventArgs> SelectionOpenWithClicked { get; init; }
    internal required EventHandler<RoutedEventArgs> SelectionSaveAsClicked { get; init; }
    internal required EventHandler<RoutedEventArgs> SelectionCancelClicked { get; init; }
    internal required EventHandler<PointerPressedEventArgs> WindowResizePointerPressed { get; init; }
    internal required EventHandler<PointerEventArgs> WindowResizePointerMoved { get; init; }
    internal required EventHandler<PointerReleasedEventArgs> WindowResizePointerReleased { get; init; }
}
