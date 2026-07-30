using System.IO.Pipes;

using Pica.Desktop.Services;
using Pica.Protocol;

namespace Pica.Desktop.Services.Background;

internal sealed class PicaBackgroundActivationClient
{
    public bool IsAvailable
    {
        get
        {
            if (!Mutex.TryOpenExisting(
                    _endpoint.AvailabilityMutexName,
                    out Mutex? availabilityMutex))
            {
                return false;
            }

            availabilityMutex.Dispose();
            return true;
        }
    }

    private static readonly TimeSpan AcknowledgementTimeout =
        TimeSpan.FromSeconds(15d);
    private static readonly TimeSpan ConnectionTimeout =
        TimeSpan.FromSeconds(1d);

    private readonly PicaBackgroundActivationEndpoint _endpoint;

    public PicaBackgroundActivationClient()
        : this(PicaBackgroundActivationEndpoint.Default)
    {
    }

    internal PicaBackgroundActivationClient(
        PicaBackgroundActivationEndpoint endpoint)
    {
        _endpoint = endpoint
            ?? throw new ArgumentNullException(nameof(endpoint));
    }

    public bool CanForward(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        return (PicaLaunchArguments.GetHostPipeName(arguments) is null)
            && IsAvailable;
    }

    public async Task ForwardAsync(
        IReadOnlyList<string> arguments,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        using NamedPipeClientStream pipe = new(
            ".",
            _endpoint.PipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        using CancellationTokenSource connectionCancellationSource =
            CancellationTokenSource.CreateLinkedTokenSource(ct);
        connectionCancellationSource.CancelAfter(ConnectionTimeout);

        try
        {
            await pipe
                .ConnectAsync(connectionCancellationSource.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException ex)
            when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException(
                "The background Pica process did not accept the activation connection in time.",
                ex);
        }

        PicaBackgroundActivationRequest request = new(arguments.ToArray());
        await PicaProtocolStream
            .WriteAsync(pipe, request, ct)
            .ConfigureAwait(false);
        using CancellationTokenSource acknowledgementCancellationSource =
            CancellationTokenSource.CreateLinkedTokenSource(ct);
        acknowledgementCancellationSource.CancelAfter(AcknowledgementTimeout);

        try
        {
            await PicaProtocolStream
                .ReadAsync<PicaBackgroundActivationAcknowledgement>(
                    pipe,
                    acknowledgementCancellationSource.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException ex)
            when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException(
                "The background Pica process did not acknowledge the activation in time.",
                ex);
        }
    }
}
