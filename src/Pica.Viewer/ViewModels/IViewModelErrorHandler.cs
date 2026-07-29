namespace Pica.Viewer.ViewModels;

internal interface IViewModelErrorHandler
{
    void Log(Exception exception, string operationName);

    string GetUserMessage(Exception exception);
}
