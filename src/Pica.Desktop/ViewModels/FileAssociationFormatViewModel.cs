using CommunityToolkit.Mvvm.ComponentModel;

namespace Pica.Desktop.ViewModels;

internal sealed class FileAssociationFormatViewModel : ObservableObject
{
    public string Extension { get; }
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                _selectionChanged();
            }
        }
    }
    public bool MatchesFilter
    {
        get => _matchesFilter;
        private set => SetProperty(ref _matchesFilter, value);
    }

    private readonly Action _selectionChanged;
    private bool _isSelected;
    private bool _matchesFilter = true;

    public FileAssociationFormatViewModel(string extension, Action selectionChanged)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);
        Extension = extension;
        _selectionChanged = selectionChanged ?? throw new ArgumentNullException(nameof(selectionChanged));
    }

    internal void ApplyFilter(string text)
    {
        MatchesFilter = Extension.Contains(text, StringComparison.OrdinalIgnoreCase);
    }
}
