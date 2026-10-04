using System.Text.Json;

using Microsoft.Extensions.Logging;

using Pica.Protocol;

namespace Pica.Desktop.Services;

internal sealed class PicaDesktopStateService : IPicaDesktopStateService
{
    private const int StateFileBufferSize = 4096;
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly SemaphoreSlim _stateLock = new(1, 1);
    private readonly string _stateFilePath;
    private readonly ILogger<PicaDesktopStateService> _logger;

    public PicaDesktopStateService(ILogger<PicaDesktopStateService> logger)
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            PicaProtocolConstants.ApplicationName, "State", "desktop.json"), logger)
    {
    }

    internal PicaDesktopStateService(string stateFilePath, ILogger<PicaDesktopStateService> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateFilePath);
        _stateFilePath = stateFilePath;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PicaDesktopState> LoadAsync(CancellationToken ct)
    {
        await _stateLock.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            return await ReadAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _stateLock.Release();
        }
    }

    public async Task SaveAsync(PicaDesktopState state, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(state);
        await UpdateAsync(current =>
        {
            current.LocalizationId = state.LocalizationId;
            current.BackgroundIdleTimeoutSeconds = state.BackgroundIdleTimeoutSeconds;
            current.IsClipboardShortcutEnabled = state.IsClipboardShortcutEnabled;
            current.IsFullscreenClipboardShortcutEnabled = state.IsFullscreenClipboardShortcutEnabled;
            current.ClipboardShortcut = state.ClipboardShortcut;
            current.HasSeenFileAssociationsPrompt = state.HasSeenFileAssociationsPrompt;
            current.HasSeenClipboardShortcutPrompt = state.HasSeenClipboardShortcutPrompt;
            current.PreviousFileAssociations = state.CreateCopy().PreviousFileAssociations;
        }, ct).ConfigureAwait(false);
    }

    public async Task UpdateAsync(Action<PicaDesktopState> update, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(update);
        await UpdateAsync((state, _) =>
        {
            update(state);
            return Task.CompletedTask;
        }, () => Task.CompletedTask, ct).ConfigureAwait(false);
    }

    public async Task UpdateAsync(Func<PicaDesktopState, CancellationToken, Task> update, Func<Task> rollback, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(update);
        ArgumentNullException.ThrowIfNull(rollback);
        await _stateLock.WaitAsync(ct).ConfigureAwait(false);
        string temporaryPath = _stateFilePath + ".tmp";

        try
        {
            string directory = Path.GetDirectoryName(_stateFilePath)
                ?? throw new InvalidOperationException("The Pica desktop state directory is unavailable.");
            Directory.CreateDirectory(directory);
            await using FileStream ownership = await AcquireFileLockAsync(ct).ConfigureAwait(false);
            PicaDesktopState current = await ReadAsync(ct).ConfigureAwait(false);

            try
            {
                await update(current, ct).ConfigureAwait(false);

                await using (FileStream stream = new(temporaryPath, FileMode.Create, FileAccess.Write,
                    FileShare.None, StateFileBufferSize, FileOptions.Asynchronous))
                {
                    await JsonSerializer.SerializeAsync(stream, current.CreateNormalizedCopy(), SerializerOptions, ct)
                        .ConfigureAwait(false);
                    await stream.FlushAsync(ct).ConfigureAwait(false);
                }

                ct.ThrowIfCancellationRequested();
                File.Move(temporaryPath, _stateFilePath, overwrite: true);
            }
            catch (Exception changeException)
            {
                try
                {
                    await rollback().ConfigureAwait(false);
                }
                catch (Exception rollbackException)
                {
                    throw new AggregateException("Pica could not restore settings after a failed change.",
                        changeException, rollbackException);
                }

                throw;
            }
        }
        finally
        {
            _stateLock.Release();
        }
    }

    private async Task<PicaDesktopState> ReadAsync(CancellationToken ct)
    {
        try
        {
            await using FileStream stream = new(_stateFilePath, FileMode.Open, FileAccess.Read,
                FileShare.Read | FileShare.Delete, StateFileBufferSize, FileOptions.Asynchronous);
            PicaDesktopState? state = await JsonSerializer.DeserializeAsync<PicaDesktopState>(
                stream, SerializerOptions, ct).ConfigureAwait(false);

            return (state ?? new PicaDesktopState()).CreateNormalizedCopy();
        }
        catch (FileNotFoundException)
        {
            return new PicaDesktopState();
        }
        catch (DirectoryNotFoundException)
        {
            return new PicaDesktopState();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Pica desktop state is invalid; using defaults");
            return new PicaDesktopState();
        }
    }

    private async Task<FileStream> AcquireFileLockAsync(CancellationToken ct)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));

        while (true)
        {
            timeout.Token.ThrowIfCancellationRequested();

            try
            {
                return new FileStream(_stateFilePath + ".lock", FileMode.OpenOrCreate,
                    FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException ex) when ((ex.HResult & 0xFFFF) is 32 or 33)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(20), timeout.Token).ConfigureAwait(false);
            }
        }
    }
}
