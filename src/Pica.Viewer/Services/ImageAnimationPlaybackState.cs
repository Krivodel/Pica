namespace Pica.Viewer.Services;

internal enum ImageAnimationPlaybackState
{
    Idle,
    InitialBuffering,
    Playing,
    RemainingBuffering,
    Completed,
    Failed
}
