using Microsoft.Extensions.Logging;

namespace Pica.Desktop.Services.Logging;

internal sealed class PicaFileLogger : ILogger
{
    private readonly string _categoryName;
    private readonly Func<IExternalScopeProvider> _scopeProviderAccessor;
    private readonly PicaRollingFileWriter _writer;

    public PicaFileLogger(
        string categoryName,
        PicaRollingFileWriter writer,
        Func<IExternalScopeProvider> scopeProviderAccessor)
    {
        _categoryName = categoryName
            ?? throw new ArgumentNullException(nameof(categoryName));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _scopeProviderAccessor = scopeProviderAccessor
            ?? throw new ArgumentNullException(nameof(scopeProviderAccessor));
    }

    public IDisposable BeginScope<TState>(TState state)
        where TState : notnull
    {
        return _scopeProviderAccessor().Push(state);
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return _writer.IsEnabled(logLevel);
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);

        if (!IsEnabled(logLevel))
        {
            return;
        }

        string message = formatter(state, exception);
        _writer.Write(
            logLevel,
            _categoryName,
            eventId,
            message,
            exception);
    }
}
