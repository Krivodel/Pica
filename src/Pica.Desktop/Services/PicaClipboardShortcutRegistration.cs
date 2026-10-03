using Microsoft.Extensions.Logging;

using Velopack.Windows;

using Pica.Desktop.Services.Background;

namespace Pica.Desktop.Services;

internal sealed class PicaClipboardShortcutRegistration
{
    internal static string StartupShortcutPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Pica Clipboard.lnk");
    internal static string ExplorerShortcutPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Pica Clipboard.lnk");

    private readonly IPicaDesktopStateService _stateService;
    private readonly ILogger _logger;
    private readonly string _startupPath;
    private readonly string _explorerPath;
    private readonly string _executablePath;
    private readonly Func<CancellationToken, Task> _refreshWindowsAsync;
    private readonly Action _clearWindowHotKeys;

    public PicaClipboardShortcutRegistration(IPicaDesktopStateService stateService,
        ILogger<PicaClipboardShortcutRegistration> logger)
        : this(stateService, logger, StartupShortcutPath, ExplorerShortcutPath,
            PicaClipboardShortcutService.ExecutablePath)
    {
    }

    internal PicaClipboardShortcutRegistration(IPicaDesktopStateService stateService, ILogger logger,
        string startupPath, string explorerPath, string executablePath,
        Func<CancellationToken, Task>? refreshWindowsAsync = null, Action? clearWindowHotKeys = null)
    {
        _stateService = stateService ?? throw new ArgumentNullException(nameof(stateService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ArgumentException.ThrowIfNullOrWhiteSpace(startupPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(explorerPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        _startupPath = startupPath;
        _explorerPath = explorerPath;
        _executablePath = executablePath;
        _refreshWindowsAsync = refreshWindowsAsync ?? RefreshWindowsAsync;
        _clearWindowHotKeys = clearWindowHotKeys ?? WindowsPicaActivationLocator.ClearShortcutHotKeys;
    }

    internal static void RemoveShortcuts()
    {
        RemoveShortcut(StartupShortcutPath);
        RemoveShortcut(ExplorerShortcutPath);
    }

    internal async Task<bool> ApplyAsync(PicaClipboardAgentRequest request, WindowsClipboardHotKey hotKey,
        bool isResident, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(hotKey);
        PicaDesktopState? previous = null;
        byte[]? startup = null;
        byte[]? explorer = null;
        bool changed = false;
        bool applied = false;

        try
        {
            await _stateService.UpdateAsync(async (state, token) =>
            {
                PicaDesktopState next = state.CreateCopy();
                request.ApplyTo(next);

                if (!isResident && (state.RequiresClipboardAgent || next.RequiresClipboardAgent))
                {
                    return;
                }

                PicaClipboardShortcutGesture candidate = request.Gesture ?? next.ClipboardShortcut;
                PicaClipboardShortcutService.ValidateGesture(candidate);
                bool ownsExplorer = !state.RequiresClipboardAgent && state.IsClipboardShortcutEnabled
                    && IsExplorerShortcut(state.ClipboardShortcut);
                bool isCurrentGesture = candidate.HasSameRegistration(state.ClipboardShortcut);
                previous = state.CreateCopy();
                startup = await ReadShortcutAsync(_startupPath, token).ConfigureAwait(false);
                explorer = await ReadShortcutAsync(_explorerPath, token).ConfigureAwait(false);

                if (!(ownsExplorer && isCurrentGesture) && ((request.Operation is PicaClipboardAgentOperation.SetGesture
                    or PicaClipboardAgentOperation.Validate) || next.IsClipboardShortcutEnabled))
                {
                    await hotKey.ValidateAsync(candidate, token).ConfigureAwait(false);
                }

                if (request.Operation == PicaClipboardAgentOperation.Validate)
                {
                    applied = true;
                    return;
                }

                changed = true;

                if (state.IsClipboardShortcutEnabled && !state.RequiresClipboardAgent
                    && (!next.IsClipboardShortcutEnabled || next.RequiresClipboardAgent
                        || (next.ClipboardShortcut != state.ClipboardShortcut) || !ownsExplorer))
                {
                    await Task.Run(() => ResetExplorerShortcut(!next.IsClipboardShortcutEnabled || next.RequiresClipboardAgent), token)
                        .ConfigureAwait(false);
                    _clearWindowHotKeys();

                    if (ownsExplorer && next.IsClipboardShortcutEnabled)
                    {
                        await WaitForExplorerReleaseAsync(hotKey, state.ClipboardShortcut, token).ConfigureAwait(false);
                    }
                }

                await hotKey.SetAsync(next.RequiresClipboardAgent ? next.ClipboardShortcut : null, token).ConfigureAwait(false);
                await Task.Run(() =>
                {
                    SaveShortcut(_startupPath, next.RequiresClipboardAgent, PicaLaunchArguments.ClipboardAgentArgument, 0);
                    SaveShortcut(_explorerPath, next.IsClipboardShortcutEnabled && !next.RequiresClipboardAgent,
                        PicaLaunchArguments.ClipboardArgument, next.ClipboardShortcut.ShellHotKey);
                }, token).ConfigureAwait(false);
                state.IsClipboardShortcutEnabled = next.IsClipboardShortcutEnabled;
                state.IsFullscreenClipboardShortcutEnabled = next.IsFullscreenClipboardShortcutEnabled;
                state.ClipboardShortcut = next.ClipboardShortcut;
                state.HasSeenClipboardShortcutPrompt = next.HasSeenClipboardShortcutPrompt;
                applied = true;
            }, async () =>
            {
                if (!changed || (previous is null))
                {
                    return;
                }

                if (!previous.RequiresClipboardAgent)
                {
                    await hotKey.SetAsync(null, CancellationToken.None).ConfigureAwait(false);
                }

                await RestoreShortcutAsync(_startupPath, startup).ConfigureAwait(false);
                await RestoreShortcutAsync(_explorerPath, explorer).ConfigureAwait(false);

                if (previous.RequiresClipboardAgent)
                {
                    await WaitForExplorerReleaseAsync(hotKey, previous.ClipboardShortcut, CancellationToken.None).ConfigureAwait(false);
                    await hotKey.SetAsync(previous.ClipboardShortcut, CancellationToken.None).ConfigureAwait(false);
                }
            }, ct).ConfigureAwait(false);
        }
        catch (Exception)
        {
            if (changed)
            {
                await RefreshWindowShortcutsAsync(CancellationToken.None).ConfigureAwait(false);
            }

            throw;
        }

        if (applied && (request.Operation != PicaClipboardAgentOperation.Validate))
        {
            await RefreshWindowShortcutsAsync(ct).ConfigureAwait(false);
        }

        return applied;
    }

    private static void RemoveShortcut(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
            WindowsShortcutNative.NotifyShortcutChanged(path, true, false);
        }
    }

    private static async Task<byte[]?> ReadShortcutAsync(string path, CancellationToken ct)
    {
        return File.Exists(path) ? await File.ReadAllBytesAsync(path, ct).ConfigureAwait(false) : null;
    }

    private static async Task RestoreShortcutAsync(string path, byte[]? contents)
    {
        if (contents is null)
        {
            RemoveShortcut(path);
            return;
        }

        bool existed = File.Exists(path);
        await File.WriteAllBytesAsync(path, contents, CancellationToken.None).ConfigureAwait(false);
        WindowsShortcutNative.NotifyShortcutChanged(path, existed, true);
    }

    private static async Task WaitForExplorerReleaseAsync(WindowsClipboardHotKey hotKey,
        PicaClipboardShortcutGesture gesture, CancellationToken ct)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));

        while (true)
        {
            try
            {
                await hotKey.ValidateAsync(gesture, ct).ConfigureAwait(false);
                return;
            }
            catch (PicaShortcutException ex) when ((ex.Failure == PicaShortcutFailure.Occupied)
                && !timeout.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100), ct).ConfigureAwait(false);
            }
        }
    }

    private bool IsExplorerShortcut(PicaClipboardShortcutGesture gesture)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Pica global clipboard activation requires Windows.");
        }

        if (!File.Exists(_explorerPath))
        {
            return false;
        }

        using ShellLink link = new(_explorerPath);
        return string.Equals(link.Target, _executablePath, StringComparison.OrdinalIgnoreCase)
            && (link.Arguments == PicaLaunchArguments.ClipboardArgument) && (link.HotKey == gesture.ShellHotKey);
    }

    private void SaveShortcut(string path, bool enabled, string arguments, short hotKey)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Pica global clipboard activation requires Windows.");
        }

        if (!enabled)
        {
            RemoveShortcut(path);
            return;
        }

        bool existed = File.Exists(path);
        // Explorer can lose the hotkey when a shortcut is deleted and recreated at the same path.
        using (ShellLink shortcut = existed ? new ShellLink(path) : new ShellLink())
        {
            if (existed && (shortcut.Target == _executablePath) && (shortcut.Arguments == arguments)
                && (shortcut.HotKey == hotKey))
            {
                return;
            }

            shortcut.Target = _executablePath;
            shortcut.Arguments = arguments;
            shortcut.WorkingDirectory = Path.GetDirectoryName(_executablePath);
            shortcut.Description = "Pica — глобальная вставка из буфера";
            shortcut.IconPath = _executablePath;
            shortcut.HotKey = hotKey;
            shortcut.Save(path);
        }

        WindowsShortcutNative.NotifyShortcutChanged(path, existed, true);
    }

    private void ResetExplorerShortcut(bool remove)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Pica global clipboard activation requires Windows.");
        }

        if (!File.Exists(_explorerPath))
        {
            return;
        }

        using (ShellLink shortcut = new(_explorerPath))
        {
            shortcut.HotKey = 0;
            shortcut.Save(_explorerPath);
        }

        WindowsShortcutNative.NotifyShortcutChanged(_explorerPath, true, true);

        if (remove)
        {
            RemoveShortcut(_explorerPath);
        }
    }

    private async Task RefreshWindowShortcutsAsync(CancellationToken ct)
    {
        try
        {
            await _refreshWindowsAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Pica could not refresh window clipboard shortcuts");
        }
    }

    private async Task RefreshWindowsAsync(CancellationToken ct)
    {
        foreach (PicaBackgroundActivationEndpoint endpoint in WindowsPicaActivationLocator.FindAll())
        {
            try
            {
                await new PicaBackgroundActivationClient(endpoint).ForwardAsync(
                    new string[] { PicaLaunchArguments.RefreshClipboardArgument }, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or TimeoutException or OperationCanceledException)
            {
                _logger.LogWarning(ex, "Pica could not refresh a viewer clipboard shortcut");
            }
        }
    }
}
