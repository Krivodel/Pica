using System;
using System.IO;
using System.Text;

using Microsoft.Extensions.Logging;

namespace Pica.Installer;

internal sealed class InstallerFileLogger<TCategory> : ILogger<TCategory>
{
    private static readonly Encoding LogEncoding = new UTF8Encoding(false);
    private static readonly object SyncRoot = new object();

    private readonly string _categoryName;

    public InstallerFileLogger()
    {
        _categoryName = typeof(TCategory).FullName
            ?? typeof(TCategory).Name;
    }

    public IDisposable BeginScope<TState>(TState state)
        where TState : notnull
    {
        return InstallerLogScope.Instance;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel != LogLevel.None;
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (formatter is null)
        {
            throw new ArgumentNullException(nameof(formatter));
        }

        if (!IsEnabled(logLevel))
        {
            return;
        }

        string logDirectory = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            InstallerProduct.DataDirectoryName,
            "Logs");
        string logPath = Path.Combine(
            logDirectory,
            "installer.log");
        string message = formatter(state, exception);
        string exceptionText = exception is null
            ? string.Empty
            : $"{Environment.NewLine}{exception}";
        string logEntry =
            $"{DateTimeOffset.Now:O} [{logLevel}] {_categoryName}: {message}{exceptionText}{Environment.NewLine}";

        lock (SyncRoot)
        {
            Directory.CreateDirectory(logDirectory);
            File.AppendAllText(logPath, logEntry, LogEncoding);
        }
    }
}
