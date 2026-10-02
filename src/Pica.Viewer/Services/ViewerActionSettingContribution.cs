using Microsoft.Extensions.Logging;

using Avalonia.Controls;

using Pica.Viewer.Controls;

namespace Pica.Viewer.Services;

public sealed class ViewerActionSettingContribution : ViewerSettingContribution
{
    private readonly Func<Window, CancellationToken, Task> _executeAsync;
    private readonly Func<Exception, string> _getErrorMessage;
    private readonly ILogger _logger;

    public ViewerActionSettingContribution(
        string label,
        Func<Window, CancellationToken, Task> executeAsync,
        Func<Exception, string> getErrorMessage,
        ILogger logger,
        ViewerSettingPlacement placement = ViewerSettingPlacement.Footer)
        : base(label, placement)
    {
        _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
        _getErrorMessage = getErrorMessage ?? throw new ArgumentNullException(nameof(getErrorMessage));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    internal override ViewerSettingControl CreateControl()
    {
        return new ViewerActionSettingControl(Label, _executeAsync, _getErrorMessage, _logger);
    }
}
