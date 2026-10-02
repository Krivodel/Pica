using System.Diagnostics;
using System.IO.Pipes;

using Microsoft.Extensions.Logging;

using Pica.Desktop.Services.Background;
using Pica.Protocol;

namespace Pica.Desktop.Services;

internal sealed class PicaClipboardAgent
{
    private readonly IPicaDesktopStateService _stateService;
    private readonly ILogger<PicaClipboardAgent> _logger;
    private readonly PicaClipboardShortcutRegistration _registration;
    private readonly string _executablePath;
    private readonly string _pipeName;
    private readonly Action _activate;

    public PicaClipboardAgent(IPicaDesktopStateService stateService, ILogger<PicaClipboardAgent> logger,
        PicaClipboardShortcutRegistration registration)
    {
        _stateService = stateService ?? throw new ArgumentNullException(nameof(stateService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _registration = registration ?? throw new ArgumentNullException(nameof(registration));
        _executablePath = PicaClipboardShortcutService.ExecutablePath;
        _pipeName = PicaClipboardShortcutService.AgentPipeName;
        _activate = Activate;
    }

    internal PicaClipboardAgent(IPicaDesktopStateService stateService, ILogger<PicaClipboardAgent> logger,
        string startupShortcutPath, string executablePath, Func<CancellationToken, Task>? refreshWindowsAsync = null,
        string? pipeName = null, Action? activate = null, Action? clearWindowHotKeys = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(startupShortcutPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        _stateService = stateService ?? throw new ArgumentNullException(nameof(stateService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _registration = new PicaClipboardShortcutRegistration(stateService, logger, startupShortcutPath,
            startupShortcutPath + ".explorer.lnk", executablePath, refreshWindowsAsync, clearWindowHotKeys);
        _executablePath = executablePath;
        _pipeName = pipeName ?? PicaClipboardShortcutService.AgentPipeName;
        _activate = activate ?? Activate;
    }

    internal async Task RunAsync(CancellationToken ct)
    {
        using Mutex availability = new(false, _pipeName + ".Available", out bool created);

        if (!created)
        {
            return;
        }

        await using WindowsClipboardHotKey hotKey = new(_activate);
        PicaDesktopState state = await _stateService.LoadAsync(ct).ConfigureAwait(false);

        try
        {
            await hotKey.SetAsync(state.RequiresClipboardAgent ? state.ClipboardShortcut : null, ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pica could not restore the global clipboard shortcut");
        }

        while (!ct.IsCancellationRequested)
        {
            using NamedPipeServerStream pipe = PicaActivationServer.CreatePipe(_pipeName);
            using CancellationTokenSource requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct);

            if (!state.RequiresClipboardAgent)
            {
                requestTimeout.CancelAfter(TimeSpan.FromSeconds(15));
            }

            try
            {
                await pipe.WaitForConnectionAsync(requestTimeout.Token).ConfigureAwait(false);
                requestTimeout.CancelAfter(TimeSpan.FromSeconds(15));
                PicaClipboardAgentRequest request = await PicaProtocolStream.ReadAsync<PicaClipboardAgentRequest>(
                    pipe, requestTimeout.Token).ConfigureAwait(false);
                PicaShortcutFailure? failure = null;

                try
                {
                    await ApplyAsync(request, hotKey, requestTimeout.Token).ConfigureAwait(false);
                }
                catch (PicaShortcutException ex)
                {
                    _logger.LogWarning(ex, "Pica clipboard shortcut command {Operation} was rejected", request.Operation);
                    failure = ex.Failure;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Pica clipboard shortcut command {Operation} failed", request.Operation);
                    failure = PicaShortcutFailure.SaveFailed;
                }

                state = await _stateService.LoadAsync(ct).ConfigureAwait(false);
                await PicaProtocolStream.WriteAsync(pipe, new PicaClipboardAgentReply(state, failure), requestTimeout.Token)
                    .ConfigureAwait(false);

                if (!state.RequiresClipboardAgent)
                {
                    return;
                }
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested && !state.RequiresClipboardAgent)
            {
                return;
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or OperationCanceledException
                or System.Text.Json.JsonException)
            {
                _logger.LogWarning(ex, "Pica clipboard agent request did not complete");
            }
        }
    }

    internal async Task ApplyAsync(PicaClipboardAgentRequest request, WindowsClipboardHotKey hotKey, CancellationToken ct)
    {
        await _registration.ApplyAsync(request, hotKey, true, ct).ConfigureAwait(false);
    }

    private void Activate()
    {
        try
        {
            PicaBackgroundActivationEndpoint? endpoint = WindowsPicaActivationLocator.FindLastActive();

            if (endpoint?.ProcessId is { } processId)
            {
                WindowsShortcutNative.AllowSetForegroundWindow(processId);
                _ = ActivateWindowAsync(endpoint);
                return;
            }

            LaunchViewer();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pica could not launch clipboard insertion");
        }
    }

    private void LaunchViewer()
    {
        using Process? process = Process.Start(new ProcessStartInfo(Environment.ProcessPath ?? _executablePath)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                ArgumentList = { PicaLaunchArguments.ClipboardArgument }
            });

        if (process is not null)
        {
            WindowsShortcutNative.AllowSetForegroundWindow(process.Id);
        }
    }

    private async Task ActivateWindowAsync(PicaBackgroundActivationEndpoint endpoint)
    {
        try
        {
            await new PicaBackgroundActivationClient(endpoint).ForwardAsync(
                new string[] { PicaLaunchArguments.ClipboardArgument }, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Pica could not activate the existing viewer; starting a clipboard viewer");

            try
            {
                LaunchViewer();
            }
            catch (Exception launchException)
            {
                _logger.LogError(launchException, "Pica could not start a clipboard viewer");
            }
        }
    }
}
