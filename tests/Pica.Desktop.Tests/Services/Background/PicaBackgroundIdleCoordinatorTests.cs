using FluentAssertions;
using Xunit;

using Pica.Desktop.Services.Background;
using Pica.Protocol;

namespace Pica.Desktop.Tests.Services.Background;

public sealed class PicaBackgroundIdleCoordinatorTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5d);

    [Fact]
    public async Task Start_WhenClientForwardsArguments_CompletesWithActivation()
    {
        PicaBackgroundActivationEndpoint endpoint = CreateEndpoint();
        using PicaBackgroundIdleCoordinator coordinator = new(endpoint);
        PicaBackgroundActivationClient client = new(endpoint);
        string[] arguments = [@"C:\Images\sample.png"];
        long sourceWindowHandle = 42L;
        coordinator.Start(TestTimeout, CancellationToken.None);

        client.CanForward(arguments).Should().BeTrue();
        Task forwardingTask = client.ForwardAsync(
            arguments,
            sourceWindowHandle,
            CancellationToken.None);
        IPicaBackgroundActivation? activation = await coordinator
            .Completion
            .WaitAsync(TestTimeout);
        await coordinator.StopAsync(CancellationToken.None);

        activation.Should().NotBeNull();
        forwardingTask.IsCompleted.Should().BeFalse();

        await using (activation)
        {
            activation.Arguments.Should().Equal(arguments);
            activation.SourceWindowHandle.Should().Be(sourceWindowHandle);
            await activation.AcknowledgeAsync(CancellationToken.None);
        }

        await forwardingTask.WaitAsync(TestTimeout);
    }

    [Fact]
    public async Task ReadAsync_WithLegacyActivationMessage_UsesNullSourceWindowHandle()
    {
        string[] arguments = [@"C:\Images\sample.png"];
        using MemoryStream stream = new();
        await PicaProtocolStream.WriteAsync(
            stream,
            new { arguments },
            CancellationToken.None);
        stream.Position = 0L;

        PicaBackgroundActivationRequest request =
            await PicaProtocolStream
                .ReadAsync<PicaBackgroundActivationRequest>(
                    stream,
                    CancellationToken.None);

        request.Arguments.Should().Equal(arguments);
        request.SourceWindowHandle.Should().BeNull();
    }

    [Fact]
    public async Task Start_WhenTimeoutIsZero_CompletesWithoutActivation()
    {
        PicaBackgroundActivationEndpoint endpoint = CreateEndpoint();
        using PicaBackgroundIdleCoordinator coordinator = new(endpoint);
        coordinator.Start(TimeSpan.Zero, CancellationToken.None);

        IPicaBackgroundActivation? activation = await coordinator
            .Completion
            .WaitAsync(TestTimeout);
        await coordinator.StopAsync(CancellationToken.None);

        activation.Should().BeNull();
    }

    [Fact]
    public async Task ForwardAsync_WhenActivationIsNotAcknowledged_ThrowsIOException()
    {
        PicaBackgroundActivationEndpoint endpoint = CreateEndpoint();
        using PicaBackgroundIdleCoordinator coordinator = new(endpoint);
        PicaBackgroundActivationClient client = new(endpoint);
        string[] arguments = [@"C:\Images\sample.png"];
        coordinator.Start(TestTimeout, CancellationToken.None);
        Task forwardingTask = client.ForwardAsync(
            arguments,
            CancellationToken.None);
        IPicaBackgroundActivation? activation = await coordinator
            .Completion
            .WaitAsync(TestTimeout);
        await coordinator.StopAsync(CancellationToken.None);
        activation.Should().NotBeNull();
        await activation.DisposeAsync();

        Func<Task> act = async () => await forwardingTask.WaitAsync(
            TestTimeout);

        await act.Should().ThrowAsync<IOException>();
    }

    [Fact]
    public async Task Start_AfterCompletedCycle_StartsAnotherCycle()
    {
        PicaBackgroundActivationEndpoint endpoint = CreateEndpoint();
        using PicaBackgroundIdleCoordinator coordinator = new(endpoint);
        coordinator.Start(TimeSpan.Zero, CancellationToken.None);
        await coordinator.Completion.WaitAsync(TestTimeout);
        await coordinator.StopAsync(CancellationToken.None);

        coordinator.Start(TimeSpan.Zero, CancellationToken.None);
        IPicaBackgroundActivation? activation = await coordinator
            .Completion
            .WaitAsync(TestTimeout);
        await coordinator.StopAsync(CancellationToken.None);

        activation.Should().BeNull();
    }

    [Fact]
    public void IsAvailable_WithoutListeningCoordinator_ReturnsFalse()
    {
        PicaBackgroundActivationEndpoint endpoint = CreateEndpoint();
        PicaBackgroundActivationClient client = new(endpoint);

        bool isAvailable = client.IsAvailable;

        isAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task CanForward_WithHostedArguments_ReturnsFalse()
    {
        PicaBackgroundActivationEndpoint endpoint = CreateEndpoint();
        using PicaBackgroundIdleCoordinator coordinator = new(endpoint);
        PicaBackgroundActivationClient client = new(endpoint);
        string[] arguments =
            [PicaProtocolConstants.PipeArgument, "sample-pipe"];
        coordinator.Start(TestTimeout, CancellationToken.None);

        bool canForward = client.CanForward(arguments);
        await coordinator.StopAsync(CancellationToken.None);

        canForward.Should().BeFalse();
        client.IsAvailable.Should().BeFalse();
    }

    private static PicaBackgroundActivationEndpoint CreateEndpoint()
    {
        string endpointSuffix = Guid.NewGuid().ToString("N");

        return new PicaBackgroundActivationEndpoint(
            $"Pica.Tests.BackgroundActivation.{endpointSuffix}",
            $"Pica.Tests.BackgroundActivation.Available.{endpointSuffix}");
    }
}
