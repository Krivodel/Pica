namespace Pica.Viewer.Services;

internal sealed record ViewerWindowPlacement(
    bool IsWindowed,
    int? WindowX,
    int? WindowY,
    double? WindowWidth,
    double? WindowHeight);
