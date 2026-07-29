using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

using CommunityToolkit.Mvvm.Input;

namespace Pica.Viewer.Controls;

internal sealed class ViewerCheckBoxSettingControl : ViewerSettingControl
{
    internal override Control Control => CheckBox;
    internal CheckBox CheckBox { get; }
    internal bool IsEnabled
    {
        get => CheckBox.IsEnabled;
        set => CheckBox.IsEnabled = value;
    }

    private readonly IAsyncRelayCommand<bool> _changedCommand;
    private bool _isChangingValue;
    private bool _currentValue;

    internal ViewerCheckBoxSettingControl(
        string content,
        bool initialValue,
        IAsyncRelayCommand<bool> changedCommand,
        bool isEnabled = true,
        double topSpacing = 0d)
        : base(null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        ArgumentOutOfRangeException.ThrowIfNegative(topSpacing);
        _changedCommand = changedCommand
            ?? throw new ArgumentNullException(nameof(changedCommand));
        _currentValue = initialValue;

        CheckBox = new CheckBox
        {
            Content = content,
            IsChecked = initialValue,
            IsEnabled = isEnabled,
            Margin = new Thickness(0d, topSpacing, 0d, 0d)
        };
        CheckBox.IsCheckedChanged += OnIsCheckedChanged;
    }

    internal void SetValue(bool value)
    {
        _currentValue = value;
        _isChangingValue = true;

        try
        {
            CheckBox.IsChecked = value;
        }
        finally
        {
            _isChangingValue = false;
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

        _currentValue = isChecked;
        await _changedCommand.ExecuteAsync(isChecked);
    }
}
