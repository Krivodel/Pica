using FluentAssertions;
using Xunit;

using Pica.Desktop.Services.Background;

namespace Pica.Desktop.Tests.Services.Background;

public sealed class PicaBackgroundIdlePreparationTests
{
    private static readonly TimeSpan TestTimeout =
        TimeSpan.FromSeconds(5d);

    [Fact]
    public async Task PrepareAsync_WhenActivationIsPending_ReclaimsMemoryOnceAfterCleanup()
    {
        RecordingIdleMemoryReclaimer memoryReclaimer = new();
        PicaBackgroundIdlePreparation preparation = new(
            memoryReclaimer);
        TaskCompletionSource closeCleanupCompletion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource activationCompletion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenSource timeout = new(TestTimeout);

        Task preparationTask = preparation.PrepareAsync(
            closeCleanupCompletion.Task,
            activationCompletion.Task,
            timeout.Token);

        memoryReclaimer.CallCount.Should().Be(0);
        preparationTask.IsCompleted.Should().BeFalse();
        closeCleanupCompletion.TrySetResult();
        await preparationTask;

        memoryReclaimer.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task PrepareAsync_WhenActivationArrivesDuringCleanup_SkipsMemoryReclamation()
    {
        RecordingIdleMemoryReclaimer memoryReclaimer = new();
        PicaBackgroundIdlePreparation preparation = new(
            memoryReclaimer);
        TaskCompletionSource closeCleanupCompletion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource activationCompletion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenSource timeout = new(TestTimeout);
        Task preparationTask = preparation.PrepareAsync(
            closeCleanupCompletion.Task,
            activationCompletion.Task,
            timeout.Token);

        activationCompletion.TrySetResult();
        closeCleanupCompletion.TrySetResult();
        await preparationTask;

        memoryReclaimer.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task PrepareAsync_WhenCleanupFails_PropagatesFailureWithoutReclamation()
    {
        RecordingIdleMemoryReclaimer memoryReclaimer = new();
        PicaBackgroundIdlePreparation preparation = new(
            memoryReclaimer);
        InvalidOperationException cleanupException = new(
            "Viewer cleanup failed.");
        Task closeCleanupCompletion =
            Task.FromException(cleanupException);
        TaskCompletionSource activationCompletion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        Func<Task> act = () => preparation.PrepareAsync(
            closeCleanupCompletion,
            activationCompletion.Task,
            CancellationToken.None);

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage(cleanupException.Message);
        memoryReclaimer.CallCount.Should().Be(0);
    }
}
