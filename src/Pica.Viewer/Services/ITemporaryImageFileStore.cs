namespace Pica.Viewer.Services;

internal interface ITemporaryImageFileStore : IDisposable
{
    string CreateChannelFilePath(ImageChannel channel);

    string CreateSelectionFilePath();

    Task SaveAsync(
        string filePath,
        PreparedClipboardImage image,
        CancellationToken ct);
}
