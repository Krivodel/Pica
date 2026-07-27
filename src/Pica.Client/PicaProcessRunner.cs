using System.Diagnostics;
using System.IO.Pipes;
using Microsoft.Extensions.Logging;

using Pica.Protocol;

namespace Pica.Client;

public sealed class PicaProcessRunner : IPicaProcessRunner
{
    private static readonly string PipeNamePrefix =
        PicaProtocolConstants.ApplicationName + ".Client.";

    private readonly ILogger<PicaProcessRunner> _logger;

    public PicaProcessRunner(ILogger<PicaProcessRunner> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task RunAsync(
        string executablePath,
        PicaViewerRequest request,
        Func<PicaActionInvocation, CancellationToken, Task> invocationHandler,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(invocationHandler);
        ct.ThrowIfCancellationRequested();

        string pipeName = PipeNamePrefix + Guid.NewGuid().ToString("N");
        await using NamedPipeServerStream pipe = new(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);
        using Process process = CreateProcess(executablePath, pipeName);

        if (!process.Start())
        {
            throw new InvalidOperationException("The installed Pica application could not be started.");
        }

        _logger.LogInformation(
            "Started Pica process for {ItemCount} images and {ActionCount} actions",
            request.Items.Count,
            request.Actions.Count);

        using CancellationTokenSource processExited = new();
        process.EnableRaisingEvents = true;
        process.Exited += OnProcessExited;

        try
        {
            using CancellationTokenSource connectionCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(ct, processExited.Token);
            await pipe.WaitForConnectionAsync(connectionCancellation.Token).ConfigureAwait(false);
            _logger.LogDebug("Pica process connected to its client session");
            await PicaProtocolStream.WriteAsync(pipe, request, connectionCancellation.Token).ConfigureAwait(false);
            _logger.LogDebug(
                "Sent Pica viewer request containing {ItemCount} images",
                request.Items.Count);

            while (!processExited.IsCancellationRequested)
            {
                PicaActionInvocation invocation;

                try
                {
                    invocation = await PicaProtocolStream
                        .ReadAsync<PicaActionInvocation>(pipe, processExited.Token)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException ex) when (processExited.IsCancellationRequested)
                {
                    _logger.LogDebug(
                        ex,
                        "Stopped reading Pica actions because the process exited");
                    break;
                }
                catch (EndOfStreamException ex)
                {
                    _logger.LogDebug(ex, "Pica action stream ended");
                    break;
                }

                _logger.LogInformation(
                    "Received Pica action {ActionId} for item {ItemId}",
                    invocation.ActionId,
                    invocation.ItemId);
                await invocationHandler(invocation, CancellationToken.None).ConfigureAwait(false);
            }

            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            _logger.LogInformation(
                "Pica process exited with code {ExitCode}",
                process.ExitCode);
        }
        finally
        {
            process.Exited -= OnProcessExited;
        }

        void OnProcessExited(object? sender, EventArgs e)
        {
            _ = sender;
            _ = e;
            processExited.Cancel();
        }
    }

    private static Process CreateProcess(string executablePath, string pipeName)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = executablePath,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add(PicaProtocolConstants.PipeArgument);
        startInfo.ArgumentList.Add(pipeName);

        return new Process
        {
            StartInfo = startInfo
        };
    }
}
