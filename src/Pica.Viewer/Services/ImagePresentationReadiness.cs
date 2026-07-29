namespace Pica.Viewer.Services;

internal sealed class ImagePresentationReadiness :
    IImagePresentationReadiness
{
    public bool IsReady =>
        _imageLoadCoordinator.IsFullResolutionReady
        && _imagePresentation.IsDisplayedBitmapReady(
            _session.SelectedChannel);

    private readonly ImageViewerSession _session;
    private readonly ImageLoadCoordinator _imageLoadCoordinator;
    private readonly ImagePresentationController _imagePresentation;

    internal ImagePresentationReadiness(
        ImageViewerSession session,
        ImageLoadCoordinator imageLoadCoordinator,
        ImagePresentationController imagePresentation)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _imageLoadCoordinator = imageLoadCoordinator
            ?? throw new ArgumentNullException(nameof(imageLoadCoordinator));
        _imagePresentation = imagePresentation
            ?? throw new ArgumentNullException(nameof(imagePresentation));
    }

    public async Task WaitAsync(CancellationToken ct)
    {
        await _imageLoadCoordinator
            .WaitForFullResolutionAsync(ct)
            .ConfigureAwait(false);

        if (!_imageLoadCoordinator.IsFullResolutionReady)
        {
            return;
        }

        await _imagePresentation
            .WaitForSelectedChannelAsync(ct)
            .ConfigureAwait(false);
    }
}
