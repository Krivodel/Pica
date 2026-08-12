namespace Pica.Viewer.Services;

internal sealed record PngChunk(
    uint Type,
    byte[] Data,
    byte[] RawContent);
