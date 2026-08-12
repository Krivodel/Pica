namespace Pica.Viewer.Services;

internal sealed record IsoBmffTopLevelContent(
    int MetaBoxOffset,
    int MovieBoxOffset)
{
    internal bool StartsWithStillImages =>
        MetaBoxOffset < MovieBoxOffset;
}
