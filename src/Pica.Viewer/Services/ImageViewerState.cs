namespace Pica.Viewer.Services;

public sealed class ImageViewerState
{
    public int BackgroundIdleTimeoutSeconds { get; set; } =
        ViewerSettingsDefaults.BackgroundIdleTimeoutSeconds;
    public bool IsCheckerboardBackgroundEnabled { get; set; } =
        ViewerSettingsDefaults.CheckerboardBackgroundEnabled;
    public bool IsFilteringEnabled { get; set; } = ViewerSettingsDefaults.FilteringEnabled;
    public int MovementSpeed { get; set; } = ViewerSettingsDefaults.MovementSpeed;
    public int ZoomSpeed { get; set; } = ViewerSettingsDefaults.ZoomSpeed;
    public bool ExpandOnDoubleClick { get; set; } = ViewerSettingsDefaults.ExpandOnDoubleClick;
    public bool IsFastLoadingEnabled { get; set; } = ViewerSettingsDefaults.FastLoadingEnabled;
    public bool AllowFreeZoomOut { get; set; } = ViewerSettingsDefaults.AllowFreeZoomOut;
    public bool IsPanningInertiaEnabled { get; set; } = ViewerSettingsDefaults.PanningInertiaEnabled;
    public WindowResizeBehavior ResizeBehavior { get; set; } = ViewerSettingsDefaults.ResizeBehavior;
    public bool RememberWindowPlacement { get; set; } = ViewerSettingsDefaults.RememberWindowPlacement;
    public bool ShowImageName { get; set; } = ViewerSettingsDefaults.ShowImageName;
    public bool ShowImageFormat { get; set; } = ViewerSettingsDefaults.ShowImageFormat;
    public bool ShowImageResolution { get; set; } = ViewerSettingsDefaults.ShowImageResolution;
    public bool ShowImageModificationDate { get; set; } =
        ViewerSettingsDefaults.ShowImageModificationDate;
    public bool? IsWindowed { get; set; }
    public int? WindowX { get; set; }
    public int? WindowY { get; set; }
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }

    internal ImageViewerState CreateCopy()
    {
        return (ImageViewerState)MemberwiseClone();
    }

    internal ImageViewerState CreateNormalizedCopy()
    {
        ImageViewerState normalizedState = CreateCopy();
        normalizedState.BackgroundIdleTimeoutSeconds =
            ViewerSettingsDefaults.NormalizeBackgroundIdleTimeoutSeconds(
                BackgroundIdleTimeoutSeconds);
        normalizedState.MovementSpeed = ViewerSettingsDefaults.NormalizeSpeed(
            MovementSpeed,
            ViewerSettingsDefaults.MovementSpeed);
        normalizedState.ZoomSpeed = ViewerSettingsDefaults.NormalizeSpeed(
            ZoomSpeed,
            ViewerSettingsDefaults.ZoomSpeed);
        normalizedState.ResizeBehavior =
            ViewerSettingsDefaults.NormalizeResizeBehavior(ResizeBehavior);
        normalizedState.IsWindowed = RememberWindowPlacement
            && (IsWindowed ?? HasCompleteWindowPlacement());
        normalizedState.WindowX = RememberWindowPlacement ? WindowX : null;
        normalizedState.WindowY = RememberWindowPlacement ? WindowY : null;
        normalizedState.WindowWidth = RememberWindowPlacement
            ? NormalizeDimension(WindowWidth)
            : null;
        normalizedState.WindowHeight = RememberWindowPlacement
            ? NormalizeDimension(WindowHeight)
            : null;

        return normalizedState;
    }

    internal ImageViewerState CreateSnapshot(
        bool isWindowed,
        int? windowX,
        int? windowY,
        double? windowWidth,
        double? windowHeight)
    {
        ImageViewerState snapshot = CreateCopy();
        snapshot.IsWindowed = RememberWindowPlacement && isWindowed;
        snapshot.WindowX = RememberWindowPlacement ? windowX : null;
        snapshot.WindowY = RememberWindowPlacement ? windowY : null;
        snapshot.WindowWidth = RememberWindowPlacement ? windowWidth : null;
        snapshot.WindowHeight = RememberWindowPlacement ? windowHeight : null;

        return snapshot;
    }

    private static double? NormalizeDimension(double? dimension)
    {
        return dimension is > 0d && double.IsFinite(dimension.Value)
            ? dimension
            : null;
    }

    private bool HasCompleteWindowPlacement()
    {
        return WindowX is not null
            && WindowY is not null
            && WindowWidth is not null
            && WindowHeight is not null;
    }
}
