using System.IO.Pipes;

using Pica.Protocol;

namespace Pica.Desktop.Services;

public sealed class PicaHostConnection : IAsyncDisposable
{
    private readonly NamedPipeClientStream _pipe;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    private PicaHostConnection(NamedPipeClientStream pipe)
    {
        _pipe = pipe;
    }

    public static async Task<PicaHostConnection> ConnectAsync(string pipeName, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
        NamedPipeClientStream pipe = new(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);

        try
        {
            await pipe.ConnectAsync(ct).ConfigureAwait(false);
            return new PicaHostConnection(pipe);
        }
        catch
        {
            await pipe.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async Task<PicaViewerRequest> ReceiveRequestAsync(CancellationToken ct)
    {
        return await PicaProtocolStream
            .ReadAsync<PicaViewerRequest>(_pipe, ct)
            .ConfigureAwait(false);
    }

    public async Task SendAsync(PicaActionInvocation invocation, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        await _writeLock.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            await PicaProtocolStream.WriteAsync(_pipe, invocation, ct).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _writeLock.Dispose();
        await _pipe.DisposeAsync().ConfigureAwait(false);
    }
}
