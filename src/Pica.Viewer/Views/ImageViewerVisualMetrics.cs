namespace Pica.Viewer.Views;

internal static class ImageViewerVisualMetrics
{
    internal const double HiddenControlsOpacity = 0d;
    internal const double VisibleControlsOpacity = 1d;
    internal const double CloseRevealSize = 64d;
    internal const double WindowControlsWidth = CloseRevealSize * 3d;
    internal const double ArrowAreaMinWidth = 24d;
    internal const double InformationPanelMargin = 16d;
    internal const double InformationRevealHeight = 96d;
    internal const double InformationRevealWidth = 560d;
    internal const double SettingsPanelHiddenOffset = -10d;
    internal const double SelectionToolbarHeight = 44d;

    internal static readonly TimeSpan SelectionOverlayFadeDuration =
        TimeSpan.FromSeconds(0.16d);
}
