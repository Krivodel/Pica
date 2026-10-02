using FluentAssertions;
using Xunit;

using Pica.Desktop.Services;
using Pica.Desktop.Services.Background;
using Pica.Protocol;

namespace Pica.Desktop.Tests.Services.Background;

public sealed class PicaActivationServerTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5d);

    [Fact]
    public async Task ReceiveAsync_WithRepeatedClipboardRequests_ReusesExistingActivationTransport()
    {
        string suffix = Guid.NewGuid().ToString("N");
        PicaBackgroundActivationEndpoint endpoint = new($"Pica.Tests.Clipboard.{suffix}", $"Pica.Tests.Clipboard.Available.{suffix}");
        PicaActivationServer server = new(endpoint);
        PicaBackgroundActivationClient client = new(endpoint);
        string[] arguments = [PicaLaunchArguments.ClipboardArgument];
        using CancellationTokenSource timeout = new(TestTimeout);

        for (int request = 0; request < 2; request++)
        {
            Task<IPicaBackgroundActivation> receiving = server.ReceiveAsync(timeout.Token);
            client.CanForward(arguments).Should().BeTrue();
            Task forwarding = client.ForwardAsync(arguments, timeout.Token);
            await using IPicaBackgroundActivation activation = await receiving;
            await activation.AcknowledgeAsync(timeout.Token);
            await forwarding;

            activation.Arguments.Should().Equal(arguments);
            PicaLaunchArguments.IsClipboard(activation.Arguments).Should().BeTrue();
        }

        client.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task ReceiveAsync_WhenCancelled_RemovesEndpointAvailability()
    {
        string suffix = Guid.NewGuid().ToString("N");
        PicaBackgroundActivationEndpoint endpoint = new($"Pica.Tests.Clipboard.{suffix}", $"Pica.Tests.Clipboard.Available.{suffix}");
        PicaActivationServer server = new(endpoint);
        PicaBackgroundActivationClient client = new(endpoint);
        using CancellationTokenSource cancellation = new();

        Task<IPicaBackgroundActivation> receiving = server.ReceiveAsync(cancellation.Token);
        client.CanForward(new string[] { PicaProtocolConstants.PipeArgument, "embedded" }).Should().BeFalse();
        cancellation.Cancel();
        Func<Task> receive = () => receiving;

        await receive.Should().ThrowAsync<OperationCanceledException>();
        client.IsAvailable.Should().BeFalse();
    }
}
