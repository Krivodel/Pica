namespace Pica.Protocol;

public sealed record PicaImageItem(
    Guid Id,
    string FilePath,
    string FileName,
    string? PreviewFilePath = null)
{
    public bool IsTemporary { get; init; }
}
