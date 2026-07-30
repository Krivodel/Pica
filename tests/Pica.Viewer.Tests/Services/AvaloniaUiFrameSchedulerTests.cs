using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using FluentAssertions;
using Xunit;

using Pica.Tests.Common;
using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.Services;

[Collection(AvaloniaHeadlessCollection.Name)]
public sealed class AvaloniaUiFrameSchedulerTests
{
    private const int TestTimeoutSeconds = 10;

    private static readonly SemaphoreSlim SessionLock = new(1, 1);

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder
            .Configure<Application>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }

    [Fact]
    public async Task RequestAnimationFrame_WhenWindowIsVisible_InvokesCallback()
    {
        await DispatchAsync(async () =>
        {
            Window window = new();
            using AvaloniaUiFrameScheduler scheduler = new(window);
            TaskCompletionSource<TimeSpan> completedFrame = new(
                TaskCreationOptions.RunContinuationsAsynchronously);

            try
            {
                window.Show();

                scheduler.RequestAnimationFrame(
                    frameTime => completedFrame.TrySetResult(frameTime));
                TimeSpan frameTime = await completedFrame.Task.WaitAsync(
                    TimeSpan.FromSeconds(TestTimeoutSeconds));

                frameTime.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
                scheduler.HasPendingFrames.Should().BeFalse();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task RequestAnimationFrame_WhenWindowIsHidden_WaitsUntilWindowIsShown()
    {
        await DispatchAsync(async () =>
        {
            Window window = new();
            using AvaloniaUiFrameScheduler scheduler = new(window);
            TaskCompletionSource completedFrame = new(
                TaskCreationOptions.RunContinuationsAsynchronously);

            try
            {
                scheduler.RequestAnimationFrame(
                    _ => completedFrame.TrySetResult());

                completedFrame.Task.IsCompleted.Should().BeFalse();
                scheduler.HasPendingFrames.Should().BeTrue();

                window.Show();
                await completedFrame.Task.WaitAsync(
                    TimeSpan.FromSeconds(TestTimeoutSeconds));

                scheduler.HasPendingFrames.Should().BeFalse();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task RequestAnimationFrame_WhenWindowIsMinimized_WaitsUntilWindowIsRestored()
    {
        await DispatchAsync(async () =>
        {
            Window window = new();
            using AvaloniaUiFrameScheduler scheduler = new(window);
            TaskCompletionSource completedFrame = new(
                TaskCreationOptions.RunContinuationsAsynchronously);

            try
            {
                window.Show();
                window.WindowState = WindowState.Minimized;

                scheduler.RequestAnimationFrame(
                    _ => completedFrame.TrySetResult());

                completedFrame.Task.IsCompleted.Should().BeFalse();
                scheduler.HasPendingFrames.Should().BeTrue();

                window.WindowState = WindowState.Normal;
                await completedFrame.Task.WaitAsync(
                    TimeSpan.FromSeconds(TestTimeoutSeconds));

                scheduler.HasPendingFrames.Should().BeFalse();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task RequestAnimationFrame_WhenCallbackRequestsAnotherFrame_InvokesBothCallbacks()
    {
        await DispatchAsync(async () =>
        {
            Window window = new();
            using AvaloniaUiFrameScheduler scheduler = new(window);
            TaskCompletionSource completedFrames = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            int completedFrameCount = 0;

            try
            {
                window.Show();

                scheduler.RequestAnimationFrame(_ =>
                {
                    completedFrameCount++;
                    scheduler.RequestAnimationFrame(_ =>
                    {
                        completedFrameCount++;
                        completedFrames.TrySetResult();
                    });
                });
                await completedFrames.Task.WaitAsync(
                    TimeSpan.FromSeconds(TestTimeoutSeconds));

                completedFrameCount.Should().Be(2);
                scheduler.HasPendingFrames.Should().BeFalse();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task CancelPendingFrames_WhenWindowIsHidden_DoesNotInvokeCallbackAfterShow()
    {
        await DispatchAsync(() =>
        {
            Window window = new();
            using AvaloniaUiFrameScheduler scheduler = new(window);
            bool wasInvoked = false;

            try
            {
                scheduler.RequestAnimationFrame(_ => wasInvoked = true);

                scheduler.CancelPendingFrames();
                window.Show();

                wasInvoked.Should().BeFalse();
                scheduler.HasPendingFrames.Should().BeFalse();
            }
            finally
            {
                window.Close();
            }

            return Task.CompletedTask;
        });
    }

    private static async Task DispatchAsync(Func<Task> action)
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(
            typeof(AvaloniaUiFrameSchedulerTests),
            SessionLock,
            action).ConfigureAwait(false);
    }
}
