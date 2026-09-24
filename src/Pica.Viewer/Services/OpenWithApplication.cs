namespace Pica.Viewer.Services;

public sealed record OpenWithApplication(
    string Identifier,
    string DisplayName,
    byte[]? IconPngContent);
