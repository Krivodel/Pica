namespace Pica.Viewer.Services;

internal sealed record ImageContentNavigationState(
    int ImageCount,
    int AnimationCount,
    ImageContentGroupKind? SelectedKind,
    int SelectedContentNumber,
    int SelectedContentCount,
    int SelectedFrameNumber,
    int FrameCount,
    bool CanNavigateContent,
    bool CanNavigateFrames)
{
    internal static ImageContentNavigationState Empty { get; } = new(
        0,
        0,
        null,
        0,
        0,
        0,
        0,
        false,
        false);
}
