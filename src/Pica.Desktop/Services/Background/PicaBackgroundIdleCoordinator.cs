using System.IO.Pipes;

using Pica.Protocol;

namespace Pica.Desktop.Services.Background;

internal sealed class PicaBackgroundIdleCoordinator :
    IPicaBackgroundIdleCoordinator,
    IDisposable
{
    public Task<IPicaBackgroundActivation?> Completion =>
        _completion
        ?? throw new InvalidOperationException(
            "Pica background idle waiting has not been started.");

    private readonly object _syncRoot = new();
    private readonly PicaBackgroundActivationEndpoint _endpoint;
    private CancellationTokenSource? _cycleCancellationSource;
    private Task<IPicaBackgroundActivation?>? _completion;
    private bool _isDisposed;

    public PicaBackgroundIdleCoordinator()
        : this(PicaBackgroundActivationEndpoint.Default)
    {
    }

    internal PicaBackgroundIdleCoordinator(
        PicaBackgroundActivationEndpoint endpoint)
    {
        _endpoint = endpoint
            ?? throw new ArgumentNullException(nameof(endpoint));
    }

    public void Start(TimeSpan idleTimeout, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(
            idleTimeout,
            TimeSpan.Zero);
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        lock (_syncRoot)
        {
            if (_completion is not null)
            {
                throw new InvalidOperationException(
                    "Pica background idle waiting is already active.");
            }

            _cycleCancellationSource =
                CancellationTokenSource.CreateLinkedTokenSource(ct);
            _completion = WaitForActivationAsync(
                idleTimeout,
                _cycleCancellationSource.Token);
        }
    }

    public void Cancel()
    {
        CancellationTokenSource? cancellationSource;

        lock (_syncRoot)
        {
            cancellationSource = _cycleCancellationSource;
        }

        cancellationSource?.Cancel();
    }

    public async Task StopAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        CancellationTokenSource? cancellationSource;
        Task<IPicaBackgroundActivation?>? completion;

        lock (_syncRoot)
        {
            cancellationSource = _cycleCancellationSource;
            completion = _completion;
            _cycleCancellationSource = null;
            _completion = null;
        }

        if ((cancellationSource is null) || (completion is null))
        {
            return;
        }

        cancellationSource.Cancel();

        try
        {
            await completion
                .WaitAsync(ct)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
            when (cancellationSource.IsCancellationRequested)
        {
        }
        finally
        {
            cancellationSource.Dispose();
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        Cancel();
        _isDisposed = true;
    }

    private static async Task ObserveConnectionCancellationAsync(
        Task connectionTask,
        CancellationToken ct)
    {
        try
        {
            await connectionTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
    }

    private async Task<IPicaBackgroundActivation?> WaitForActivationAsync(
        TimeSpan idleTimeout,
        CancellationToken ct)
    {
        NamedPipeServerStream pipe = CreatePipe();
        bool isPipeOwnershipTransferred = false;

        try
        {
            using Mutex availabilityMutex = new(
                false,
                _endpoint.AvailabilityMutexName);
            using CancellationTokenSource connectionCancellationSource =
                CancellationTokenSource.CreateLinkedTokenSource(ct);
            Task connectionTask = pipe.WaitForConnectionAsync(
                connectionCancellationSource.Token);
            Task timeoutTask = Task.Delay(idleTimeout, ct);
            Task completedTask = await Task
                .WhenAny(connectionTask, timeoutTask)
                .ConfigureAwait(false);

            if (completedTask == timeoutTask)
            {
                await timeoutTask.ConfigureAwait(false);
                connectionCancellationSource.Cancel();
                await ObserveConnectionCancellationAsync(
                    connectionTask,
                    connectionCancellationSource.Token).ConfigureAwait(false);

                return null;
            }

            await connectionTask.ConfigureAwait(false);
            PicaBackgroundActivationRequest request =
                await PicaProtocolStream
                    .ReadAsync<PicaBackgroundActivationRequest>(
                        pipe,
                        ct)
                    .ConfigureAwait(false);
            PicaBackgroundActivation activation = new(
                request.Arguments,
                request.SourceWindowHandle,
                pipe);
            isPipeOwnershipTransferred = true;

            return activation;
        }
        finally
        {
            if (!isPipeOwnershipTransferred)
            {
                await pipe.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private NamedPipeServerStream CreatePipe()
    {
        return new NamedPipeServerStream(
            _endpoint.PipeName,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
    }
}
