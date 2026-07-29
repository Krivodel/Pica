using Avalonia.Controls;
using Avalonia.Layout;

using CommunityToolkit.Mvvm.Input;

namespace Pica.Viewer.Controls;

internal sealed class ViewerChoiceSettingControl<TValue> : ViewerSettingControl
{
    internal override Control Control => ComboBox;
    internal ComboBox ComboBox { get; }

    private readonly IAsyncRelayCommand<TValue> _changedCommand;
    private readonly IReadOnlyList<ViewerSettingOption<TValue>> _options;
    private TValue _currentValue;
    private bool _isChangingValue;

    internal ViewerChoiceSettingControl(
        string label,
        IReadOnlyList<ViewerSettingOption<TValue>> options,
        TValue initialValue,
        IAsyncRelayCommand<TValue> changedCommand)
        : base(label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentNullException.ThrowIfNull(options);
        _changedCommand = changedCommand
            ?? throw new ArgumentNullException(nameof(changedCommand));
        _options = options;
        _currentValue = initialValue;

        ComboBox = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = options,
            SelectedItem = options.First(
                option => EqualityComparer<TValue>.Default.Equals(option.Value, initialValue))
        };
        ComboBox.SelectionChanged += OnSelectionChanged;
    }

    private async void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _ = sender;
        _ = e;

        if (_isChangingValue)
        {
            return;
        }

        if (ComboBox.SelectedItem is not ViewerSettingOption<TValue> selectedOption)
        {
            return;
        }

        if (!_changedCommand.CanExecute(selectedOption.Value))
        {
            RestoreCurrentSelection();
            return;
        }

        _currentValue = selectedOption.Value;
        await _changedCommand.ExecuteAsync(selectedOption.Value);
    }

    private void RestoreCurrentSelection()
    {
        _isChangingValue = true;

        try
        {
            ComboBox.SelectedItem = _options.First(option =>
                EqualityComparer<TValue>.Default.Equals(
                    option.Value,
                    _currentValue));
        }
        finally
        {
            _isChangingValue = false;
        }
    }
}
