using CommunityToolkit.Mvvm.Input;

using Pica.Viewer.Controls;

namespace Pica.Viewer.Services;

public sealed class ViewerChoiceSettingContribution<TValue> :
    ViewerSettingContribution
    where TValue : notnull
{
    public IReadOnlyList<ViewerSettingChoice<TValue>> Choices { get; }
    public TValue InitialValue { get; }

    private readonly Func<TValue, CancellationToken, Task> _changeAsync;
    private readonly IReadOnlyList<ViewerSettingOption<TValue>> _options;

    public ViewerChoiceSettingContribution(
        string label,
        IReadOnlyList<ViewerSettingChoice<TValue>> choices,
        TValue initialValue,
        Func<TValue, CancellationToken, Task> changeAsync)
        : base(label)
    {
        ArgumentNullException.ThrowIfNull(choices);
        _changeAsync = changeAsync
            ?? throw new ArgumentNullException(nameof(changeAsync));

        if (choices.Count == 0)
        {
            throw new ArgumentException(
                "At least one viewer setting choice is required.",
                nameof(choices));
        }

        ViewerSettingChoice<TValue>[] copiedChoices = choices.ToArray();
        bool containsInitialValue = copiedChoices.Any(choice =>
            EqualityComparer<TValue>.Default.Equals(
                choice.Value,
                initialValue));

        if (!containsInitialValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(initialValue),
                initialValue,
                "The initial viewer setting value must be one of the choices.");
        }

        Choices = Array.AsReadOnly(copiedChoices);
        InitialValue = initialValue;
        _options = copiedChoices
            .Select(choice => new ViewerSettingOption<TValue>(
                choice.Value,
                choice.DisplayName))
            .ToArray();
    }

    public Task ApplyAsync(TValue value, CancellationToken ct)
    {
        bool containsValue = Choices.Any(choice =>
            EqualityComparer<TValue>.Default.Equals(
                choice.Value,
                value));

        if (!containsValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "The viewer setting value must be one of the choices.");
        }

        return _changeAsync(value, ct);
    }

    internal override ViewerSettingControl CreateControl()
    {
        AsyncRelayCommand<TValue> changeCommand = new(
            ExecuteChangeAsync);

        return new ViewerChoiceSettingControl<TValue>(
            Label,
            _options,
            InitialValue,
            changeCommand);
    }

    private async Task ExecuteChangeAsync(
        TValue? value,
        CancellationToken ct)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        await ApplyAsync(value, ct).ConfigureAwait(false);
    }
}
