using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace Pica.Viewer.Controls;

internal sealed class ViewerCheckBoxSettingControl : ViewerSettingControl
{
    internal override Control Control => _panel ?? (Control)CheckBox;
    internal CheckBox CheckBox { get; }
    internal TextBlock? ErrorText => _error;
    internal StackPanel? DependentSettingsPanel => _dependentPanel;
    internal override Task Completion => Task.WhenAll(
        _dependentSettings.Select(setting => setting.Completion)
            .Append(_changeCompletion));
    internal bool IsEnabled
    {
        get => CheckBox.IsEnabled;
        set => CheckBox.IsEnabled = value;
    }

    private readonly IAsyncRelayCommand<bool> _changedCommand;
    private readonly ILogger? _logger;
    private readonly StackPanel? _panel;
    private readonly ViewerSettingErrorControl? _error;
    private readonly StackPanel? _dependentPanel;
    private readonly IReadOnlyList<ViewerSettingControl> _dependentSettings;
    private readonly Func<bool>? _getCurrentValue;
    private bool _isChangingValue;
    private bool _currentValue;
    private Task _changeCompletion = Task.CompletedTask;

    internal ViewerCheckBoxSettingControl(
        string content,
        bool initialValue,
        IAsyncRelayCommand<bool> changedCommand,
        bool isEnabled = true,
        double topSpacing = 0d,
        ILogger? logger = null,
        Func<Exception, string>? getErrorMessage = null,
        bool wrapContent = false,
        IReadOnlyList<ViewerSettingControl>? dependentSettings = null,
        Func<bool>? getCurrentValue = null)
        : base(null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        ArgumentOutOfRangeException.ThrowIfNegative(topSpacing);
        _changedCommand = changedCommand
            ?? throw new ArgumentNullException(nameof(changedCommand));
        _currentValue = initialValue;
        _logger = logger;
        _dependentSettings = dependentSettings ?? Array.Empty<ViewerSettingControl>();
        _getCurrentValue = getCurrentValue;

        CheckBox = new CheckBox
        {
            Content = content,
            IsChecked = initialValue,
            IsEnabled = isEnabled,
            Margin = new Thickness(0d, topSpacing, 0d, 0d)
        };
        CheckBox.IsCheckedChanged += OnIsCheckedChanged;

        if (wrapContent)
        {
            CheckBox.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            CheckBox.Content = new TextBlock { Text = content, TextWrapping = TextWrapping.Wrap };
        }

        if (getErrorMessage is not null)
        {
            _error = new ViewerSettingErrorControl(getErrorMessage,
                logger ?? throw new ArgumentNullException(nameof(logger)));
        }

        if (dependentSettings is { Count: > 0 })
        {
            _dependentPanel = ViewerSettingsContentControl.CreateContent(dependentSettings, false);
            _dependentPanel.IsVisible = initialValue;
            _dependentPanel.Margin = new Thickness(0d, ErrorSpacing, 0d, 0d);
        }

        if ((_error is not null) || (_dependentPanel is not null))
        {
            _panel = new StackPanel { Spacing = ErrorSpacing };
            _panel.Children.Add(CheckBox);

            if (_error is not null)
            {
                _panel.Children.Add(_error);
            }

            if (_dependentPanel is not null)
            {
                _panel.Children.Add(_dependentPanel);
            }
        }
    }

    internal void SetValue(bool value)
    {
        _currentValue = value;
        _isChangingValue = true;

        try
        {
            CheckBox.IsChecked = value;

            if (_dependentPanel is not null)
            {
                _dependentPanel.IsVisible = value;
            }
        }
        finally
        {
            _isChangingValue = false;
        }
    }

    internal override void RefreshValue()
    {
        if ((_getCurrentValue is not null) && !_changedCommand.IsRunning)
        {
            SetValue(_getCurrentValue());
        }

        foreach (ViewerSettingControl setting in _dependentSettings)
        {
            setting.RefreshValue();
        }
    }

    private async void OnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        if (_isChangingValue)
        {
            return;
        }

        bool isChecked = CheckBox.IsChecked == true;

        if (!_changedCommand.CanExecute(isChecked))
        {
            SetValue(_currentValue);
            return;
        }

        bool wasEnabled = CheckBox.IsEnabled;
        CheckBox.IsEnabled = false;
        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _changeCompletion = completion.Task;

        try
        {
            _error?.ClearError();
            await _changedCommand.ExecuteAsync(isChecked);
            SetValue(isChecked);
        }
        catch (OperationCanceledException)
        {
            SetValue(_currentValue);
        }
        catch (Exception ex) when (_logger is not null)
        {
            if (_error is not null)
            {
                _error.ShowError(ex);
            }
            else
            {
                _logger.LogError(ex, "Pica could not change a checkbox setting");
            }

            SetValue(_currentValue);
        }
        finally
        {
            CheckBox.IsEnabled = wasEnabled;
            completion.TrySetResult();
        }
    }
}
