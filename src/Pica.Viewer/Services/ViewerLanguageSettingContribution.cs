using Krivodeling.Localization.Avalonia;
using Pica.Viewer.Controls;
using Pica.Viewer.ViewModels;

namespace Pica.Viewer.Services;

public sealed class ViewerLanguageSettingContribution : ViewerSettingContribution
{
    private readonly ILocalizationService _localization;
    private readonly Func<string, CancellationToken, Task> _selectAsync;
    private readonly IViewModelErrorHandler _errorHandler;
    private readonly string _searchPlaceholderKey;

    public ViewerLanguageSettingContribution(string label, ILocalizationService localization,
        Func<string, CancellationToken, Task> selectAsync, IViewModelErrorHandler errorHandler,
        string searchPlaceholderKey)
        : base(label)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _selectAsync = selectAsync ?? throw new ArgumentNullException(nameof(selectAsync));
        _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
        ArgumentException.ThrowIfNullOrWhiteSpace(searchPlaceholderKey);
        _searchPlaceholderKey = searchPlaceholderKey;
    }

    internal override ViewerSettingControl CreateControl()
    {
        ViewerLanguageSettingViewModel model = new(_localization, _selectAsync, _errorHandler);

        return new ViewerLanguageSettingControl(Label, model, _searchPlaceholderKey);
    }
}
