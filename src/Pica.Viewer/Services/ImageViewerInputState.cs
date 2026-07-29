namespace Pica.Viewer.Services;

internal sealed record ImageViewerInputState(
    bool IsSettingsVisible,
    bool HasAreaSelection,
    bool IsChannelModeActive);
