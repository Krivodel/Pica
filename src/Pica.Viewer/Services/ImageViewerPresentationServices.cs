namespace Pica.Viewer.Services;

internal sealed class ImageViewerPresentationServices : IDisposable
{
    internal ImagePresentationController Presentation { get; }
    internal ImageAnimationPlaybackController AnimationPlayback { get; }
    internal ImageLoadCoordinator LoadCoordinator { get; }
    internal IImagePresentationReadiness Readiness { get; }

    internal ImageViewerPresentationServices(
        ImagePresentationController presentation,
        ImageAnimationPlaybackController animationPlayback,
        ImageLoadCoordinator loadCoordinator,
        IImagePresentationReadiness readiness)
    {
        Presentation = presentation
            ?? throw new ArgumentNullException(nameof(presentation));
        AnimationPlayback = animationPlayback
            ?? throw new ArgumentNullException(nameof(animationPlayback));
        LoadCoordinator = loadCoordinator
            ?? throw new ArgumentNullException(nameof(loadCoordinator));
        Readiness = readiness
            ?? throw new ArgumentNullException(nameof(readiness));
    }

    public void Dispose()
    {
        AnimationPlayback.Dispose();
        LoadCoordinator.Dispose();
        Presentation.Dispose();
    }

    internal async Task DisposeAsync(CancellationToken ct)
    {
        List<Exception> failures = [];

        try
        {
            await AnimationPlayback
                .DisposeAsync(ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            failures.Add(ex);
        }

        try
        {
            await LoadCoordinator
                .DisposeAsync(ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            failures.Add(ex);
        }

        try
        {
            await Presentation
                .DisposeAsync(ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            failures.Add(ex);
        }

        if (failures.Count > 0)
        {
            throw new AggregateException(
                "Failed to dispose Pica image presentation services.",
                failures);
        }
    }
}
