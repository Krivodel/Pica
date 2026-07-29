using Pica.Viewer.ViewModels;

namespace Pica.Viewer.Tests.TestDoubles;

internal sealed class RecordingViewModelErrorHandler :
    IViewModelErrorHandler
{
    internal const string SafeMessage = "Безопасное сообщение";

    internal Exception? LastException { get; private set; }
    internal string? LastOperationName { get; private set; }

    public void Log(Exception exception, string operationName)
    {
        LastException = exception
            ?? throw new ArgumentNullException(nameof(exception));
        LastOperationName = operationName;
    }

    public string GetUserMessage(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return SafeMessage;
    }
}
