namespace Pica.Viewer.Services;

internal enum ImageAnimationPlaybackState
{
    Idle,
    InitialBuffering,
    Playing,
    Paused,
    Seeking,
    RemainingBuffering,
    Completed,
    Failed
}
