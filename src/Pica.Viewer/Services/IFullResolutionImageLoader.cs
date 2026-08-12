namespace Pica.Viewer.Services;

internal interface IFullResolutionImageLoader
{
    Task<DecodedImageContent> LoadAsync(
        string fullPath,
        CancellationToken ct);
}
