using System.Globalization;
using System.Text;

using Microsoft.Extensions.Logging;

namespace Pica.Desktop.Services.Logging;

internal sealed class PicaRollingFileWriter : IDisposable
{
    private const string FileNamePrefix = "pica-";
    private const string FileNameSearchPattern = "pica-*.log";
    private const string LogRetentionCleanupOperationName =
        "log retention cleanup";

    private readonly LogLevel _minimumLevel;
    private readonly long _maxFileSizeBytes;
    private readonly int _retainedFileCount;
    private readonly int _retentionDays;
    private readonly PicaLogEntryFormatter _entryFormatter;
    private readonly string _logDirectoryPath;
    private readonly object _syncRoot = new();
    private StreamWriter? _writer;
    private DateOnly _currentDate;
    private int _currentSequence;
    private long _currentFileSizeBytes;
    private bool _isDisposed;
    private bool _isAvailable;

    public PicaRollingFileWriter(
        string logDirectoryPath,
        PicaFileLoggingOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logDirectoryPath);
        ArgumentNullException.ThrowIfNull(options);
        ValidateOptions(options);

        _logDirectoryPath = logDirectoryPath;
        _minimumLevel = options.MinimumLevel;
        _maxFileSizeBytes = options.MaxFileSizeBytes;
        _retainedFileCount = options.RetainedFileCount;
        _retentionDays = options.RetentionDays;
        _entryFormatter = new PicaLogEntryFormatter(options);

        try
        {
            Directory.CreateDirectory(_logDirectoryPath);
            _isAvailable = true;
        }
        catch (IOException)
        {
            _isAvailable = false;
        }
        catch (UnauthorizedAccessException)
        {
            _isAvailable = false;
        }
        catch (NotSupportedException)
        {
            _isAvailable = false;
        }
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return _isAvailable
            && !_isDisposed
            && logLevel >= _minimumLevel
            && logLevel != LogLevel.None;
    }

    public void Write(
        LogLevel logLevel,
        string categoryName,
        EventId eventId,
        string message,
        Exception? exception)
    {
        string entry = _entryFormatter.Format(
            logLevel,
            categoryName,
            eventId,
            message,
            exception);
        int entrySizeBytes = Encoding.UTF8.GetByteCount(entry);

        lock (_syncRoot)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            try
            {
                WriteEntry(entry, entrySizeBytes);
            }
            catch (IOException)
            {
                Disable();
            }
            catch (UnauthorizedAccessException)
            {
                Disable();
            }
            catch (NotSupportedException)
            {
                Disable();
            }
            catch (ObjectDisposedException)
            {
                Disable();
            }
        }
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            DisposeWriterSilently();
        }
    }

    private static int TryParseSequence(string? fileName, string prefix)
    {
        if (string.IsNullOrWhiteSpace(fileName)
            || !fileName.StartsWith(prefix, StringComparison.Ordinal)
            || !int.TryParse(
                fileName.AsSpan(prefix.Length),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int sequence))
        {
            return 0;
        }

        return sequence;
    }

    private static void ValidateOptions(PicaFileLoggingOptions options)
    {
        if (options.MaxFileSizeBytes <= 0
            || options.MaximumExceptionDepth <= 0
            || options.MaximumMessageCharacters <= 0
            || options.MaximumSanitizedMessageCharacters <= 0
            || options.MaximumSanitizerInputMessageCharacters
                < options.MaximumSanitizedMessageCharacters
            || options.MaximumStackFrameCount <= 0
            || options.RetainedFileCount <= 0
            || options.RetentionDays <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "File logging options must contain positive limits.");
        }
    }

    private void EnsureWriter(int entrySizeBytes)
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.Now);
        bool requiresNewFile = _writer is null
            || today != _currentDate
            || (_currentFileSizeBytes > 0
                && _currentFileSizeBytes + entrySizeBytes
                    > _maxFileSizeBytes);

        if (!requiresNewFile)
        {
            return;
        }

        DisposeWriterSilently();
        _currentDate = today;
        FileStream stream = CreateLogFile(today, out string filePath);
        _writer = new StreamWriter(stream, new UTF8Encoding(false));
        _currentFileSizeBytes = 0;
        DeleteExpiredFiles(filePath);
    }

    private FileStream CreateLogFile(
        DateOnly date,
        out string filePath)
    {
        string prefix = $"{FileNamePrefix}{date:yyyyMMdd}-";
        _currentSequence = FindHighestSequence(prefix) + 1;

        while (true)
        {
            filePath = Path.Combine(
                _logDirectoryPath,
                $"{prefix}{_currentSequence:D3}.log");

            try
            {
                return new FileStream(
                    filePath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.Read);
            }
            catch (IOException) when (File.Exists(filePath))
            {
                _currentSequence++;
            }
        }
    }

    private int FindHighestSequence(string prefix)
    {
        return Directory
            .GetFiles(_logDirectoryPath, $"{prefix}*.log")
            .Select(Path.GetFileNameWithoutExtension)
            .Select(fileName => TryParseSequence(fileName, prefix))
            .DefaultIfEmpty(0)
            .Max();
    }

    private void WriteEntry(string entry, int entrySizeBytes)
    {
        EnsureWriter(entrySizeBytes);
        _writer?.Write(entry);
        _writer?.Flush();
        _currentFileSizeBytes += entrySizeBytes;
    }

    private void DeleteExpiredFiles(string currentFilePath)
    {
        string[] retainedCandidates = Directory
            .GetFiles(_logDirectoryPath, FileNameSearchPattern)
            .Where(path => !string.Equals(
                path,
                currentFilePath,
                StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToArray();
        DateTime retentionCutoffUtc =
            DateTime.UtcNow.AddDays(-_retentionDays);
        string[] expiredFiles = retainedCandidates
            .Where(path =>
                File.GetLastWriteTimeUtc(path) < retentionCutoffUtc)
            .Concat(retainedCandidates.Skip(_retainedFileCount - 1))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (string expiredFile in expiredFiles)
        {
            TryDeleteExpiredFile(expiredFile);
        }
    }

    private void TryDeleteExpiredFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException ex)
        {
            WriteInternalFailure(
                LogRetentionCleanupOperationName,
                ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            WriteInternalFailure(
                LogRetentionCleanupOperationName,
                ex);
        }
        catch (NotSupportedException ex)
        {
            WriteInternalFailure(
                LogRetentionCleanupOperationName,
                ex);
        }
    }

    private void WriteInternalFailure(
        string operation,
        Exception exception)
    {
        _writer?.WriteLine(
            "{0:O} [Warning] Pica.Desktop.Logging: {1} failed. ExceptionType={2}",
            DateTimeOffset.Now,
            operation,
            exception.GetType().FullName);
        _writer?.Flush();
    }

    private void Disable()
    {
        _isAvailable = false;
        DisposeWriterSilently();
    }

    private void DisposeWriterSilently()
    {
        try
        {
            _writer?.Dispose();
        }
        catch (IOException)
        {
        }
        catch (ObjectDisposedException)
        {
        }

        _writer = null;
    }
}
