using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

namespace Pica.Desktop.Services.Logging;

internal sealed class PicaFileLoggerProvider :
    ILoggerProvider,
    ISupportExternalScope
{
    private readonly ConcurrentDictionary<string, PicaFileLogger> _loggers;
    private readonly PicaRollingFileWriter _writer;
    private IExternalScopeProvider _scopeProvider =
        new LoggerExternalScopeProvider();

    public PicaFileLoggerProvider(
        string logDirectoryPath,
        PicaFileLoggingOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logDirectoryPath);
        ArgumentNullException.ThrowIfNull(options);

        _loggers = new ConcurrentDictionary<string, PicaFileLogger>(
            StringComparer.Ordinal);
        _writer = new PicaRollingFileWriter(logDirectoryPath, options);
    }

    public ILogger CreateLogger(string categoryName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryName);

        return _loggers.GetOrAdd(
            categoryName,
            category => new PicaFileLogger(
                category,
                _writer,
                () => _scopeProvider));
    }

    public void Dispose()
    {
        _writer.Dispose();
    }

    public void SetScopeProvider(IExternalScopeProvider scopeProvider)
    {
        _scopeProvider = scopeProvider
            ?? throw new ArgumentNullException(nameof(scopeProvider));
    }
}
