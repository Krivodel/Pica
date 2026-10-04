using Avalonia;
using Avalonia.Controls;

namespace Pica.Viewer.Behaviors;

public static class ComboBoxSearchBehavior
{
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<ComboBox, bool>(
            "IsEnabled",
            typeof(ComboBoxSearchBehavior));
    public static readonly AttachedProperty<string?> TextProperty =
        AvaloniaProperty.RegisterAttached<ComboBox, string?>(
            "Text",
            typeof(ComboBoxSearchBehavior));
    public static readonly AttachedProperty<string?> PlaceholderTextProperty =
        AvaloniaProperty.RegisterAttached<ComboBox, string?>(
            "PlaceholderText",
            typeof(ComboBoxSearchBehavior));

    private const string SearchableOptionsClass = "searchable-options";

    static ComboBoxSearchBehavior()
    {
        IsEnabledProperty.Changed.AddClassHandler<ComboBox>(
            OnIsEnabledChanged);
    }

    public static bool GetIsEnabled(ComboBox comboBox)
    {
        return AttachedPropertyValueAccessor.Get(comboBox, IsEnabledProperty);
    }

    public static void SetIsEnabled(ComboBox comboBox, bool value)
    {
        AttachedPropertyValueAccessor.Set(comboBox, IsEnabledProperty, value);
    }

    public static string? GetText(ComboBox comboBox)
    {
        return AttachedPropertyValueAccessor.Get(comboBox, TextProperty);
    }

    public static void SetText(ComboBox comboBox, string? value)
    {
        AttachedPropertyValueAccessor.Set(comboBox, TextProperty, value);
    }

    public static string? GetPlaceholderText(ComboBox comboBox)
    {
        return AttachedPropertyValueAccessor.Get(
            comboBox,
            PlaceholderTextProperty);
    }

    public static void SetPlaceholderText(
        ComboBox comboBox,
        string? value)
    {
        AttachedPropertyValueAccessor.Set(
            comboBox,
            PlaceholderTextProperty,
            value);
    }

    private static void OnIsEnabledChanged(
        ComboBox comboBox,
        AvaloniaPropertyChangedEventArgs args)
    {
        if (args.NewValue is true)
        {
            comboBox.Classes.Add(SearchableOptionsClass);
            return;
        }

        comboBox.Classes.Remove(SearchableOptionsClass);
    }
}
