using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Krivodeling.Localization.Avalonia;

namespace Pica.Viewer.ViewModels;

internal sealed partial class ViewerLanguageSettingViewModel : ObservableObject, IDisposable
{
    public IReadOnlyList<ViewerLanguageOptionViewModel> Options => _options;
    public ViewerLanguageOptionViewModel? SelectedOption
    {
        get => _selectedOption;
        set
        {
            if (SetProperty(ref _selectedOption, value) && !_isSynchronizing && (value is not null))
            {
                ApplyCommand.Execute(null);
            }
        }
    }
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value ?? string.Empty))
            {
                ApplySearch();
            }
        }
    }
    public bool HasErrorMessage => !string.IsNullOrWhiteSpace(ErrorMessage);

    private readonly ILocalizationService _localization;
    private readonly Func<string, CancellationToken, Task> _selectAsync;
    private readonly IViewModelErrorHandler _errorHandler;
    private IReadOnlyList<ViewerLanguageOptionViewModel> _options = Array.Empty<ViewerLanguageOptionViewModel>();
    private ViewerLanguageOptionViewModel? _selectedOption;
    private bool _isSynchronizing;
    private bool _isListening;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    private bool _isLoading;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrorMessage))]
    private string? _errorMessage;
    private string _searchText = string.Empty;

    internal ViewerLanguageSettingViewModel(ILocalizationService localization,
        Func<string, CancellationToken, Task> selectAsync, IViewModelErrorHandler errorHandler)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _selectAsync = selectAsync ?? throw new ArgumentNullException(nameof(selectAsync));
        _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
        RefreshSelection();
    }

    public void Dispose()
    {
        Stop();
    }

    internal void Start()
    {
        if (_isListening)
        {
            return;
        }

        _localization.Changed += OnLocalizationChanged;
        _isListening = true;
        RefreshSelection();
    }

    internal void Stop()
    {
        _localization.Changed -= OnLocalizationChanged;
        _isListening = false;
        ApplyCommand.Cancel();
        RefreshCommand.Cancel();
    }

    internal void RefreshSelection()
    {
        _isSynchronizing = true;

        try
        {
            IReadOnlyList<LocalizationOption> available = _localization.AvailableLocalizations;

            if (!_options.Select(option => option.Localization).SequenceEqual(available))
            {
                SelectedOption = null;
                _options = available.Select(option => new ViewerLanguageOptionViewModel(option)).ToArray();
                OnPropertyChanged(nameof(Options));
            }

            ApplySearch();
            SelectedOption = _options.FirstOrDefault(option => string.Equals(option.Localization.Id,
                _localization.CurrentLocalization?.Id, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _isSynchronizing = false;
        }
    }

    private bool CanChange()
    {
        return !IsLoading;
    }

    [RelayCommand(CanExecute = nameof(CanChange))]
    private async Task ApplyAsync(CancellationToken ct)
    {
        if (SelectedOption is not { } selected)
        {
            return;
        }

        await RunAsync(() => _selectAsync(selected.Localization.Id, ct), ct);
    }

    [RelayCommand(CanExecute = nameof(CanChange))]
    private async Task RefreshAsync(CancellationToken ct)
    {
        await RunAsync(async () =>
        {
            await _localization.RefreshAvailableLocalizationsAsync(ct);
            _localization.ReconcileCurrentOrSystemDefault();
        }, ct);
    }

    private async Task RunAsync(Func<Task> operation, CancellationToken ct)
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            await operation();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _errorHandler.Log(exception, nameof(RunAsync));
            ErrorMessage = _errorHandler.GetUserMessage(exception);
        }
        finally
        {
            RefreshSelection();
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ClearSearch()
    {
        SearchText = string.Empty;
    }

    private void ApplySearch()
    {
        foreach (ViewerLanguageOptionViewModel option in _options)
        {
            option.ApplySearch(SearchText);
        }
    }

    private void OnLocalizationChanged(object? sender, EventArgs e)
    {
        if (!IsLoading)
        {
            RefreshSelection();
        }
    }
}
