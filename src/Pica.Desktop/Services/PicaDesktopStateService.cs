using System.Text.Json;

using Microsoft.Extensions.Logging;

using Pica.Protocol;

namespace Pica.Desktop.Services;

internal sealed class PicaDesktopStateService : IPicaDesktopStateService
{
    private const int StateFileBufferSize = 4096;
    private const string StateDirectoryName = "State";
    private const string StateFileName = "desktop.json";

    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };

    private readonly SemaphoreSlim _stateLock = new(1, 1);
    private readonly string _stateFilePath;
    private readonly ILogger<PicaDesktopStateService> _logger;
    private PicaDesktopState? _currentState;

    public PicaDesktopStateService(
        ILogger<PicaDesktopStateService> logger)
        : this(CreateDefaultStateFilePath(), logger)
    {
    }

    internal PicaDesktopStateService(
        string stateFilePath,
        ILogger<PicaDesktopStateService> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateFilePath);
        ArgumentNullException.ThrowIfNull(logger);

        _stateFilePath = stateFilePath;
        _logger = logger;
    }

    public async Task<PicaDesktopState> LoadAsync(CancellationToken ct)
    {
        await _stateLock.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            if (_currentState is not null)
            {
                _logger.LogDebug("Returning cached Pica desktop state");
                return _currentState.CreateCopy();
            }

            FileStream? openedStream = await OpenReadStreamAsync(
                _stateFilePath,
                ct).ConfigureAwait(false);

            if (openedStream is null)
            {
                _currentState = new PicaDesktopState();
                _logger.LogInformation(
                    "Pica desktop state does not exist; using defaults");
                return _currentState.CreateCopy();
            }

            await using FileStream stream = openedStream;
            PicaDesktopState? state = await JsonSerializer
                .DeserializeAsync<PicaDesktopState>(
                    stream,
                    SerializerOptions,
                    ct)
                .ConfigureAwait(false);
            _currentState = (state ?? new PicaDesktopState())
                .CreateNormalizedCopy();
            _logger.LogInformation(
                "Loaded and normalized Pica desktop state");

            return _currentState.CreateCopy();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(
                ex,
                "Pica desktop state is invalid; using defaults");
            _currentState = new PicaDesktopState();
            return _currentState.CreateCopy();
        }
        finally
        {
            _stateLock.Release();
        }
    }

    public async Task SaveAsync(
        PicaDesktopState state,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(state);
        await _stateLock.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            PicaDesktopState normalizedState =
                state.CreateNormalizedCopy();
            string? directoryPath = Path.GetDirectoryName(
                _stateFilePath);

            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                throw new InvalidOperationException(
                    "The Pica desktop state directory could not be determined.");
            }

            await using FileStream stream = await OpenWriteStreamAsync(
                _stateFilePath,
                directoryPath,
                ct).ConfigureAwait(false);
            await JsonSerializer.SerializeAsync(
                stream,
                normalizedState,
                SerializerOptions,
                ct).ConfigureAwait(false);
            _currentState = normalizedState;
            _logger.LogInformation(
                "Saved Pica desktop background idle timeout");
        }
        finally
        {
            _stateLock.Release();
        }
    }

    private static string CreateDefaultStateFilePath()
    {
        string localApplicationData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);

        return Path.Combine(
            localApplicationData,
            PicaProtocolConstants.ApplicationName,
            StateDirectoryName,
            StateFileName);
    }

    private static Task<FileStream?> OpenReadStreamAsync(
        string filePath,
        CancellationToken ct)
    {
        return Task.Run(
            () =>
            {
                if (!File.Exists(filePath))
                {
                    return null;
                }

                return new FileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    StateFileBufferSize,
                    FileOptions.Asynchronous);
            },
            ct);
    }

    private static Task<FileStream> OpenWriteStreamAsync(
        string filePath,
        string directoryPath,
        CancellationToken ct)
    {
        return Task.Run(
            () =>
            {
                Directory.CreateDirectory(directoryPath);

                return new FileStream(
                    filePath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    StateFileBufferSize,
                    FileOptions.Asynchronous);
            },
            ct);
    }
}
