using Microsoft.Extensions.Logging;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Threading;

namespace Pica.Viewer.Controls;

internal sealed class ViewerSettingErrorControl : TextBlock
{
    private readonly Func<Exception, string> _getMessage;
    private readonly ILogger _logger;

    internal ViewerSettingErrorControl(Func<Exception, string> getMessage, ILogger logger)
    {
        _getMessage = getMessage ?? throw new ArgumentNullException(nameof(getMessage));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        Classes.Add("viewer-error");
        this.Bind(ForegroundProperty, new DynamicResourceExtension("ViewerErrorForegroundBrush"));
        IsVisible = false;
        TextWrapping = TextWrapping.Wrap;
    }

    internal void ClearError()
    {
        IsVisible = false;
        Text = null;
    }

    internal void ShowError(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        _logger.LogError(exception, "Pica could not apply a setting");
        Text = _getMessage(exception);
        IsVisible = true;
        Dispatcher.Post(() => this.BringIntoView(), DispatcherPriority.Loaded);
    }
}
