using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;

using Microsoft.Extensions.Logging;

namespace Pica.Desktop.Services.Logging;

internal sealed class PicaLogEntryFormatter
{
    private readonly int _maximumExceptionDepth;
    private readonly int _maximumMessageCharacters;
    private readonly int _maximumStackFrameCount;
    private readonly PicaExceptionMessageSanitizer _exceptionMessageSanitizer;

    public PicaLogEntryFormatter(PicaFileLoggingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _maximumExceptionDepth = options.MaximumExceptionDepth;
        _maximumMessageCharacters = options.MaximumMessageCharacters;
        _maximumStackFrameCount = options.MaximumStackFrameCount;
        _exceptionMessageSanitizer =
            new PicaExceptionMessageSanitizer(options);
    }

    public string Format(
        LogLevel logLevel,
        string categoryName,
        EventId eventId,
        string message,
        Exception? exception)
    {
        StringBuilder builder = new();
        builder.Append(
            DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture));
        builder.Append(" [");
        builder.Append(logLevel);
        builder.Append("] ");
        builder.Append(categoryName);

        if (eventId.Id != 0)
        {
            builder.Append(" EventId=");
            builder.Append(
                eventId.Id.ToString(CultureInfo.InvariantCulture));
        }

        builder.Append(": ");
        builder.AppendLine(NormalizeMessage(message));
        AppendSafeException(builder, exception, 0);

        return builder.ToString();
    }

    private string NormalizeMessage(string message)
    {
        string normalizedMessage = message
            .Replace('\r', ' ')
            .Replace('\n', ' ');

        return normalizedMessage.Length <= _maximumMessageCharacters
            ? normalizedMessage
            : normalizedMessage[.._maximumMessageCharacters];
    }

    private void AppendSafeException(
        StringBuilder builder,
        Exception? exception,
        int depth)
    {
        if (exception is null
            || depth >= _maximumExceptionDepth)
        {
            return;
        }

        builder.Append("ExceptionType=");
        builder.Append(exception.GetType().FullName);
        builder.Append(" HResult=0x");
        builder.AppendLine(
            exception.HResult.ToString("X8", CultureInfo.InvariantCulture));

        string? sanitizedMessage =
            _exceptionMessageSanitizer.Sanitize(exception.Message);

        if (sanitizedMessage is not null)
        {
            builder.Append("ExceptionMessage=");
            builder.AppendLine(sanitizedMessage);
        }

        StackFrame[] frames = new StackTrace(exception, false)
            .GetFrames()
            .Take(_maximumStackFrameCount)
            .ToArray();

        foreach (StackFrame frame in frames)
        {
            MethodBase? method = frame.GetMethod();

            if (method is null)
            {
                continue;
            }

            builder.Append("   at ");
            builder.Append(method.DeclaringType?.FullName ?? "<unknown>");
            builder.Append('.');
            builder.AppendLine(method.Name);
        }

        AppendSafeException(builder, exception.InnerException, depth + 1);
    }
}
