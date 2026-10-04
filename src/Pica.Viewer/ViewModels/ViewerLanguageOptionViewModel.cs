using CommunityToolkit.Mvvm.ComponentModel;

using Krivodeling.Localization.Avalonia;

namespace Pica.Viewer.ViewModels;

internal sealed class ViewerLanguageOptionViewModel : ObservableObject
{
    public LocalizationOption Localization { get; }
    public string DisplayName => Localization.Id;
    public bool IsSearchMatch
    {
        get => _isSearchMatch;
        private set => SetProperty(ref _isSearchMatch, value);
    }

    private bool _isSearchMatch = true;

    public ViewerLanguageOptionViewModel(LocalizationOption localization)
    {
        Localization = localization ?? throw new ArgumentNullException(nameof(localization));
    }

    public void ApplySearch(string? searchText)
    {
        IsSearchMatch = Localization.MatchesSearch(searchText);
    }
}
