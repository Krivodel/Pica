namespace Pica.Viewer.Services;

internal interface IImageAnimationDelayScheduler
{
    Task DelayAsync(TimeSpan duration, CancellationToken ct);
}
