using Avalonia.Controls;
using Avalonia.Layout;

namespace Pica.Viewer.Controls;

internal sealed class ViewerChoiceSettingControl<TValue> : ViewerSettingControl
{
    internal override Control Control => ComboBox;
    internal ComboBox ComboBox { get; }

    private readonly Func<TValue, Task> _changed;

    internal ViewerChoiceSettingControl(
        string label,
        IReadOnlyList<ViewerSettingOption<TValue>> options,
        TValue initialValue,
        Func<TValue, Task> changed)
        : base(label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentNullException.ThrowIfNull(options);
        _changed = changed ?? throw new ArgumentNullException(nameof(changed));

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

        if (ComboBox.SelectedItem is not ViewerSettingOption<TValue> selectedOption)
        {
            return;
        }

        await _changed(selectedOption.Value);
    }
}
