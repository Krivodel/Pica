using System.Diagnostics;
using System.IO.Pipes;

using Avalonia.Input;
using Avalonia.Win32.Input;

using Pica.Desktop.Services.Background;
using Pica.Protocol;
using Pica.Viewer.Services;

namespace Pica.Desktop.Services;

internal sealed class PicaClipboardShortcutService
{
    internal static string AgentPipeName => PicaBackgroundActivationEndpoint.Default.PipeName + ".Clipboard";
    internal static string ExecutablePath
    {
        get
        {
            string executable = Environment.ProcessPath
                ?? throw new InvalidOperationException("Pica executable path is unavailable.");
            DirectoryInfo? directory = Directory.GetParent(executable);
            string? root = directory?.Parent?.FullName;
            string? stablePath = root is null ? null : Path.Combine(root, Path.GetFileName(executable));

            return (directory?.Name == "current") && (stablePath is not null) && File.Exists(stablePath)
                ? stablePath : executable;
        }
    }
    internal PicaDesktopState CurrentState { get; private set; } = new();

    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);
    private readonly string _pipeName;
    private readonly Action _startAgent;
    private readonly IPicaDesktopStateService? _stateService;
    private readonly PicaClipboardShortcutRegistration? _registration;
    private readonly SemaphoreSlim _operationLock = new(1, 1);

    public PicaClipboardShortcutService(IPicaDesktopStateService stateService, PicaClipboardShortcutRegistration registration)
        : this(AgentPipeName, StartAgent, stateService, registration)
    {
        ArgumentNullException.ThrowIfNull(stateService);
        ArgumentNullException.ThrowIfNull(registration);
    }

    internal PicaClipboardShortcutService(string pipeName, Action startAgent,
        IPicaDesktopStateService? stateService = null, PicaClipboardShortcutRegistration? registration = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
        _pipeName = pipeName;
        _startAgent = startAgent ?? throw new ArgumentNullException(nameof(startAgent));
        _stateService = stateService;
        _registration = registration;
    }

    internal static void ValidateGesture(PicaClipboardShortcutGesture gesture)
    {
        ArgumentNullException.ThrowIfNull(gesture);
        gesture.Validate();
        PhysicalKey physicalKey = KeyInterop.PhysicalKeyFromVirtualKey(gesture.VirtualKey, gesture.KeyData);

        if (ViewerKeyboardShortcutPolicy.ConflictsWithClipboard(gesture.Key, physicalKey, gesture.ToKeyModifiers()))
        {
            throw new PicaShortcutException(PicaShortcutFailure.ViewerConflict);
        }
    }

    internal static void StartAgent()
    {
        using Process? process = Process.Start(new ProcessStartInfo(ExecutablePath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            ArgumentList = { PicaLaunchArguments.ClipboardAgentArgument }
        });
    }

    internal void Refresh(PicaDesktopState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        CurrentState = state.CreateCopy();
    }

    internal async Task ApplyAsync(PicaClipboardAgentRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Gesture is { } gesture)
        {
            ValidateGesture(gesture);
        }

        await _operationLock.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            while (true)
            {
                if ((_stateService is not null) && (_registration is not null) && !IsAgentRunning())
                {
                    await using WindowsClipboardHotKey probe = new(() => { });
                    bool applied = await _registration.ApplyAsync(request, probe, false, ct).ConfigureAwait(false);

                    if (applied)
                    {
                        CurrentState = await _stateService.LoadAsync(ct).ConfigureAwait(false);
                        return;
                    }
                }

                if (await ApplyThroughAgentAsync(request, ct).ConfigureAwait(false))
                {
                    return;
                }
            }
        }
        finally
        {
            _operationLock.Release();
        }
    }

    private bool IsAgentRunning()
    {
        if (!Mutex.TryOpenExisting(_pipeName + ".Available", out Mutex? availability))
        {
            return false;
        }

        availability.Dispose();
        return true;
    }

    private async Task<bool> ApplyThroughAgentAsync(PicaClipboardAgentRequest request, CancellationToken ct)
    {
        using NamedPipeClientStream pipe = new(".", _pipeName, PipeDirection.InOut,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

        try
        {
            await pipe.ConnectAsync((int)ConnectTimeout.TotalMilliseconds, ct).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            if (!IsAgentRunning())
            {
                if (_stateService is not null)
                {
                    PicaDesktopState state = await _stateService.LoadAsync(ct).ConfigureAwait(false);
                    PicaDesktopState next = state.CreateCopy();
                    request.ApplyTo(next);

                    if (!state.RequiresClipboardAgent && !next.RequiresClipboardAgent)
                    {
                        return false;
                    }
                }

                _startAgent();
            }

            await pipe.ConnectAsync((int)RequestTimeout.TotalMilliseconds, ct).ConfigureAwait(false);
        }

        ct.ThrowIfCancellationRequested();
        using CancellationTokenSource timeout = new(RequestTimeout);
        await PicaProtocolStream.WriteAsync(pipe, request, timeout.Token).ConfigureAwait(false);
        PicaClipboardAgentReply reply = await PicaProtocolStream.ReadAsync<PicaClipboardAgentReply>(pipe, timeout.Token)
            .ConfigureAwait(false);
        CurrentState = reply.State;

        if (!reply.State.RequiresClipboardAgent)
        {
            while (IsAgentRunning())
            {
                await Task.Delay(TimeSpan.FromMilliseconds(20), timeout.Token).ConfigureAwait(false);
            }
        }

        if (reply.Error is { } error)
        {
            throw new PicaShortcutException(error);
        }

        return true;
    }
}
