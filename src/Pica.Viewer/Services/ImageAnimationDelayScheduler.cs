namespace Pica.Viewer.Services;

internal sealed class ImageAnimationDelayScheduler :
    IImageAnimationDelayScheduler
{
    public Task DelayAsync(
        TimeSpan duration,
        CancellationToken ct)
    {
        return Task.Delay(duration, ct);
    }
}
