using Pica.Viewer.ViewModels;

namespace Pica.Desktop.Tests.ViewModels;

internal sealed class RecordingViewModelErrorHandler : IViewModelErrorHandler
{
    internal Exception? LastException { get; private set; }

    public void Log(Exception exception, string operationName)
    {
        LastException = exception;
    }

    public string GetUserMessage(Exception exception)
    {
        return "Безопасное сообщение";
    }
}
