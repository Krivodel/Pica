using FluentAssertions;
using Xunit;

using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.Services;

public sealed class ViewerAnimationFrameSchedulerTests
{
    [Fact]
    public void RequestAnimationFrame_WhenNotPresented_SubmitsAfterPresentationResumes()
    {
        List<Action<TimeSpan>> submittedFrames = [];
        ViewerAnimationFrameScheduler scheduler =
            CreateScheduler(submittedFrames);
        int completedFrameCount = 0;

        scheduler.RequestAnimationFrame(_ => completedFrameCount++);

        submittedFrames.Should().BeEmpty();

        scheduler.SetPresentation(true);

        submittedFrames.Should().ContainSingle();
        submittedFrames[0](TimeSpan.FromMilliseconds(16));
        completedFrameCount.Should().Be(1);
        scheduler.HasPendingFrames.Should().BeFalse();
    }

    [Fact]
    public void SetPresentation_WhenFrameWasSubmitted_IgnoresStaleFrame()
    {
        List<Action<TimeSpan>> submittedFrames = [];
        ViewerAnimationFrameScheduler scheduler =
            CreateScheduler(submittedFrames);
        int completedFrameCount = 0;
        scheduler.SetPresentation(true);
        scheduler.RequestAnimationFrame(_ => completedFrameCount++);

        scheduler.SetPresentation(false);
        scheduler.SetPresentation(true);

        submittedFrames.Should().HaveCount(2);

        submittedFrames[0](TimeSpan.FromMilliseconds(16));
        completedFrameCount.Should().Be(0);

        submittedFrames[1](TimeSpan.FromMilliseconds(32));
        completedFrameCount.Should().Be(1);
        scheduler.HasPendingFrames.Should().BeFalse();
    }

    [Fact]
    public void CancelPendingFrames_WhenStaleFrameArrives_DoesNotInvokeCallback()
    {
        List<Action<TimeSpan>> submittedFrames = [];
        ViewerAnimationFrameScheduler scheduler =
            CreateScheduler(submittedFrames);
        int completedFrameCount = 0;
        scheduler.SetPresentation(true);
        scheduler.RequestAnimationFrame(_ => completedFrameCount++);

        scheduler.CancelPendingFrames();
        submittedFrames[0](TimeSpan.FromMilliseconds(16));

        completedFrameCount.Should().Be(0);
        scheduler.HasPendingFrames.Should().BeFalse();
    }

    private static ViewerAnimationFrameScheduler CreateScheduler(
        List<Action<TimeSpan>> submittedFrames)
    {
        ViewerAnimationFrameScheduler scheduler = new();
        scheduler.AnimationFrameRequested += (_, e) =>
            submittedFrames.Add(e.FrameAction);

        return scheduler;
    }
}
