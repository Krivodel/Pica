namespace Pica.Protocol;

public sealed record PicaActionDefinition(
    string Id,
    string DisplayName,
    string IconGeometry,
    double IconRotationDegrees,
    PicaActionTargets Targets,
    int Order);
