namespace Pica.Viewer.Services;

internal interface IImageFileMetadataProvider
{
    Task<DateTime?> GetModificationDateAsync(
        string filePath,
        CancellationToken ct);
}
