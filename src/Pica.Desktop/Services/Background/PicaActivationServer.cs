using System.IO.Pipes;

using Pica.Protocol;

namespace Pica.Desktop.Services.Background;

internal sealed class PicaActivationServer
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15d);
    private readonly PicaBackgroundActivationEndpoint _endpoint;

    internal PicaActivationServer(PicaBackgroundActivationEndpoint endpoint)
    {
        _endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
    }

    internal static NamedPipeServerStream CreatePipe(string pipeName)
    {
        return new NamedPipeServerStream(pipeName, PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
    }

    internal async Task<IPicaBackgroundActivation> ReceiveAsync(CancellationToken ct)
    {
        NamedPipeServerStream pipe = CreatePipe(_endpoint.PipeName);
        bool transferred = false;

        try
        {
            using Mutex availability = new(false, _endpoint.AvailabilityMutexName);
            await pipe.WaitForConnectionAsync(ct).ConfigureAwait(false);
            using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(RequestTimeout);
            PicaBackgroundActivationRequest request = await PicaProtocolStream
                .ReadAsync<PicaBackgroundActivationRequest>(pipe, timeout.Token).ConfigureAwait(false);
            PicaBackgroundActivation activation = new(request.Arguments, request.SourceWindowHandle, pipe);
            transferred = true;

            return activation;
        }
        finally
        {
            if (!transferred)
            {
                await pipe.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

}
