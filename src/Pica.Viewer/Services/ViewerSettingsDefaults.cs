namespace Pica.Viewer.Services;

internal static class ViewerSettingsDefaults
{
    public const int MovementSpeed = 3;
    public const int ZoomSpeed = 4;
    public const bool CheckerboardBackgroundEnabled = true;
    public const bool FilteringEnabled = true;
    public const bool ExpandOnDoubleClick = true;
    public const bool FastLoadingEnabled = false;
    public const bool AllowFreeZoomOut = false;
    public const bool PanningInertiaEnabled = true;
    public const bool RememberWindowPlacement = true;
    public const bool ShowImageName = false;
    public const bool ShowImageFormat = true;
    public const bool ShowImageResolution = true;
    public const bool ShowImageModificationDate = true;
    public const WindowResizeBehavior ResizeBehavior = WindowResizeBehavior.AlwaysFitImage;

    public static int MinimumSpeed => SpeedValues[0];
    public static IReadOnlyList<int> SpeedValues { get; } = [1, 2, 3, 4];

    public static int NormalizeSpeed(int speed, int defaultSpeed)
    {
        return SpeedValues.Contains(speed)
            ? speed
            : defaultSpeed;
    }

    public static WindowResizeBehavior NormalizeResizeBehavior(WindowResizeBehavior resizeBehavior)
    {
        return Enum.IsDefined(resizeBehavior)
            ? resizeBehavior
            : ResizeBehavior;
    }
}
