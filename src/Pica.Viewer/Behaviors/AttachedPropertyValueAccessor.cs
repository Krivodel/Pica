using Avalonia;

namespace Pica.Viewer.Behaviors;

internal static class AttachedPropertyValueAccessor
{
    public static TValue Get<TValue>(
        AvaloniaObject target,
        AvaloniaProperty<TValue> property)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(property);

        return target.GetValue<TValue>(property);
    }

    public static void Set<TValue>(
        AvaloniaObject target,
        AvaloniaProperty<TValue> property,
        TValue value)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(property);

        target.SetValue(property, value);
    }
}
