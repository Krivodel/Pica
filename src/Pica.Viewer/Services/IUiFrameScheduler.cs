namespace Pica.Viewer.Services;

public interface IUiFrameScheduler
{
    void RequestAnimationFrame(Action<TimeSpan> frameAction);
}
