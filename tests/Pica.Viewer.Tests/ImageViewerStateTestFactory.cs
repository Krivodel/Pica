using Pica.Viewer.Services;

namespace Pica.Viewer.Tests;

internal static class ImageViewerStateTestFactory
{
    public static ImageViewerState CreateRememberedPlacementState()
    {
        return new ImageViewerState
        {
            IsFilteringEnabled = false,
            MovementSpeed = 2,
            ZoomSpeed = 1,
            ExpandOnDoubleClick = false,
            IsFastLoadingEnabled = true,
            AllowFreeZoomOut = true,
            IsPanningInertiaEnabled = true,
            ResizeBehavior = WindowResizeBehavior.FitWhenWindowed,
            RememberWindowPlacement = true,
            ShowImageName = false,
            ShowImageFormat = false,
            ShowImageResolution = false,
            ShowImageModificationDate = true,
            IsWindowed = true,
            WindowX = -1200,
            WindowY = 80,
            WindowWidth = 900d,
            WindowHeight = 506.25d
        };
    }
}
