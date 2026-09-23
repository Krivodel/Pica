using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using FluentAssertions;
using Xunit;

using Pica.Tests.Common;
using Pica.Viewer.Tests;
using Pica.Viewer.Views;

namespace Pica.Viewer.Tests.Views;

[Collection(AvaloniaHeadlessCollection.Name)]
public sealed class ViewerActionGateTests
{
    private static readonly SemaphoreSlim SessionLock = new(1, 1);

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder
            .Configure<ViewerTestApplication>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task RunAsync_WhileActionIsRunning_AppliesWindowInteractionPolicy(
        bool blockWindowInteraction,
        bool expectedHitTestVisibility)
    {
        await DispatchAsync(async () =>
        {
            Border interactionRoot = new();
            ViewerActionGate gate = new(interactionRoot);
            TaskCompletionSource completion =
                new(TaskCreationOptions.RunContinuationsAsynchronously);

            Task action = gate.RunAsync(
                _ => completion.Task,
                CancellationToken.None,
                blockWindowInteraction);

            gate.IsRunning.Should().BeTrue();
            interactionRoot.IsHitTestVisible.Should().Be(
                expectedHitTestVisibility);

            completion.SetResult();
            await action;

            gate.IsRunning.Should().BeFalse();
            interactionRoot.IsHitTestVisible.Should().BeTrue();
        });
    }

    [Fact]
    public async Task RunAsync_WhenWindowRemainsInteractive_PreventsConcurrentAction()
    {
        await DispatchAsync(async () =>
        {
            Border interactionRoot = new();
            ViewerActionGate gate = new(interactionRoot);
            TaskCompletionSource completion =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            bool secondActionStarted = false;
            Task firstAction = gate.RunAsync(
                _ => completion.Task,
                CancellationToken.None,
                blockWindowInteraction: false);

            await gate.RunAsync(
                _ =>
                {
                    secondActionStarted = true;
                    return Task.CompletedTask;
                },
                CancellationToken.None,
                blockWindowInteraction: false);

            secondActionStarted.Should().BeFalse();
            interactionRoot.IsHitTestVisible.Should().BeTrue();

            completion.SetResult();
            await firstAction;

            gate.IsRunning.Should().BeFalse();
        });
    }

    private static async Task DispatchAsync(Func<Task> action)
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(
            typeof(ViewerActionGateTests),
            SessionLock,
            action).ConfigureAwait(false);
    }
}
