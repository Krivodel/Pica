namespace Pica.Viewer.Services;

internal static class ImageViewerInputPolicy
{
    internal static ViewerEscapeAction ResolveEscapeAction(
        ImageViewerInputState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.IsSettingsVisible)
        {
            return ViewerEscapeAction.HideSettings;
        }

        if (state.HasAreaSelection)
        {
            return ViewerEscapeAction.CancelAreaSelection;
        }

        if (state.IsChannelModeActive)
        {
            return ViewerEscapeAction.ExitChannelMode;
        }

        return ViewerEscapeAction.CloseViewer;
    }
}
