using Pica.Viewer.Services;

namespace Pica.Viewer.Views;

internal sealed class ViewerFrameAnimationRunner
{
    private readonly ViewerAnimationFrameScheduler _animationFrameScheduler;

    internal ViewerFrameAnimationRunner(
        ViewerAnimationFrameScheduler animationFrameScheduler)
    {
        _animationFrameScheduler = animationFrameScheduler
            ?? throw new ArgumentNullException(nameof(animationFrameScheduler));
    }

    internal static double EaseOutCubic(double progress)
    {
        return 1d - Math.Pow(1d - progress, 3d);
    }

    internal void Start(
        TimeSpan duration,
        Func<bool> isCurrent,
        Action<double> applyFrame,
        Action? cancelled = null,
        Action? completed = null)
    {
        ArgumentNullException.ThrowIfNull(isCurrent);
        ArgumentNullException.ThrowIfNull(applyFrame);
        DateTimeOffset startedAt = DateTimeOffset.UtcNow;
        _animationFrameScheduler.RequestAnimationFrame(OnFrame);

        void OnFrame(TimeSpan frameTime)
        {
            _ = frameTime;

            if (!isCurrent())
            {
                cancelled?.Invoke();
                return;
            }

            double elapsed =
                (DateTimeOffset.UtcNow - startedAt).TotalSeconds;
            double progress = Math.Clamp(
                elapsed / duration.TotalSeconds,
                0d,
                1d);
            applyFrame(progress);

            if (progress < 1d)
            {
                _animationFrameScheduler.RequestAnimationFrame(OnFrame);
                return;
            }

            completed?.Invoke();
        }
    }
}
