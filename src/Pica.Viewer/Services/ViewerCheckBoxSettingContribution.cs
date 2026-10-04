using Microsoft.Extensions.Logging;

using CommunityToolkit.Mvvm.Input;

using Pica.Viewer.Controls;

namespace Pica.Viewer.Services;

public sealed class ViewerCheckBoxSettingContribution : ViewerSettingContribution
{
    public bool InitialValue { get; }
    public IReadOnlyList<ViewerSettingContribution> DependentSettings { get; }

    private readonly Func<bool, CancellationToken, Task> _changeAsync;
    private readonly ILogger _logger;
    private readonly Func<Exception, string>? _getErrorMessage;
    private readonly bool _wrapContent;
    private readonly Func<bool>? _getCurrentValue;

    public ViewerCheckBoxSettingContribution(
        string label,
        bool initialValue,
        Func<bool, CancellationToken, Task> changeAsync,
        ILogger logger,
        Func<Exception, string>? getErrorMessage = null,
        bool wrapContent = false,
        IReadOnlyList<ViewerSettingContribution>? dependentSettings = null,
        Func<bool>? getCurrentValue = null)
        : base(label)
    {
        InitialValue = initialValue;
        DependentSettings = dependentSettings?.ToArray() ?? Array.Empty<ViewerSettingContribution>();
        _changeAsync = changeAsync ?? throw new ArgumentNullException(nameof(changeAsync));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _getErrorMessage = getErrorMessage;
        _wrapContent = wrapContent;
        _getCurrentValue = getCurrentValue;
    }

    public async Task ApplyAsync(bool value, CancellationToken ct)
    {
        await _changeAsync(value, ct).ConfigureAwait(false);
    }

    internal override ViewerSettingControl CreateControl()
    {
        AsyncRelayCommand<bool> command = new(ApplyAsync);

        return new ViewerCheckBoxSettingControl(Label, InitialValue, command,
            logger: _logger, getErrorMessage: _getErrorMessage, wrapContent: _wrapContent,
            dependentSettings: DependentSettings.Select(setting => setting.CreateLocalizedControl()).ToArray(),
            getCurrentValue: _getCurrentValue);
    }
}
