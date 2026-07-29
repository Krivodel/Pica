using Microsoft.Extensions.Logging;

using Pica.Viewer.Resources;
using Pica.Viewer.ViewModels;

namespace Pica.Viewer.Services;

internal sealed class ViewModelErrorHandler : IViewModelErrorHandler
{
    private readonly ILogger<ViewModelErrorHandler> _logger;

    public ViewModelErrorHandler(ILogger<ViewModelErrorHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void Log(Exception exception, string operationName)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        _logger.LogError(
            exception,
            "Pica ViewModel operation failed: {OperationName}",
            operationName);
    }

    public string GetUserMessage(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return ViewerUiStrings.OperationFailed;
    }
}
