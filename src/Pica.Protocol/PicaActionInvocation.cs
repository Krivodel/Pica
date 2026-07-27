namespace Pica.Protocol;

public sealed record PicaActionInvocation(
    string ActionId,
    Guid ItemId,
    string FilePath,
    string FileName,
    string ContentType);
