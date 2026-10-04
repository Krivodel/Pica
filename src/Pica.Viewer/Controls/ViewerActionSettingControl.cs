using Microsoft.Extensions.Logging;

using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;

using Krivodeling.Localization.Avalonia;

namespace Pica.Viewer.Controls;

internal sealed class ViewerActionSettingControl : ViewerSettingControl
{
    internal override Control Control => _panel;

    private readonly StackPanel _panel;
    private readonly ViewerSettingErrorControl _error;
    private readonly Func<Window, CancellationToken, Task> _executeAsync;
    private readonly AsyncRelayCommand _command;

    internal ViewerActionSettingControl(
        string label,
        Func<Window, CancellationToken, Task> executeAsync,
        Func<Exception, string> getErrorMessage,
        ILogger logger)
        : base(null)
    {
        _executeAsync = executeAsync;
        _error = new ViewerSettingErrorControl(getErrorMessage, logger);
        _command = new AsyncRelayCommand(ExecuteAsync);
        Button button = new() { Content = label, Command = _command };
        _panel = new StackPanel { Spacing = ErrorSpacing };
        _panel.Children.Add(button);
        _panel.Children.Add(_error);
        _panel.DetachedFromVisualTree += (_, _) => _command.Cancel();
    }

    internal override void ApplyLocalization(string key)
    {
        LocalizationBinding.Bind(_panel.Children.OfType<Button>().Single(), ContentControl.ContentProperty, key);
    }

    private async Task ExecuteAsync(CancellationToken ct)
    {
        _error.ClearError();

        try
        {
            Window owner = TopLevel.GetTopLevel(_panel) as Window
                ?? throw new InvalidOperationException("The setting action must be attached to a window.");
            await _executeAsync(owner, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _error.ShowError(ex);
        }
    }
}
