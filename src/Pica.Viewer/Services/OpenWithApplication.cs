namespace Pica.Viewer.Services;

internal sealed record OpenWithApplication(
    string Identifier,
    string DisplayName,
    byte[]? IconPngContent);
