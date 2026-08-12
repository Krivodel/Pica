namespace Pica.Viewer.Services;

internal sealed record ImageContentGroupDefinition(
    ImageContentGroupKind Kind,
    int ItemCount,
    ImageFrameNumbering FrameNumbering =
        ImageFrameNumbering.Forward);
