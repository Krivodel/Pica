using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Pica.Desktop.Resources;
using Pica.Desktop.Services.FileAssociations;
using Pica.Viewer.ViewModels;

namespace Pica.Desktop.ViewModels;

internal sealed partial class FileAssociationsViewModel : ObservableObject
{
    public ReadOnlyCollection<FileAssociationFormatViewModel> Formats { get; }
    public string SelectionSummary => string.Format(DesktopUiStrings.FileAssociationsSelectionSummaryFormat,
        Formats.Count(format => format.IsSelected), Formats.Count);
    public bool HasErrorMessage => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);
    public bool HasMatchingFormats => Formats.Any(format => format.MatchesFilter);
    public string CloseButtonText => HasStatusMessage ? DesktopUiStrings.Close : _initialCloseButtonText;

    public event EventHandler? CloseRequested;

    private readonly IPicaFileAssociationService _service;
    private readonly IViewModelErrorHandler _errorHandler;
    private readonly string _initialCloseButtonText;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    [NotifyCanExecuteChangedFor(nameof(SelectAllCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearSelectionCommand))]
    [NotifyCanExecuteChangedFor(nameof(CloseCommand))]
    private bool _isLoading;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrorMessage))]
    private string? _errorMessage;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatusMessage))]
    [NotifyPropertyChangedFor(nameof(CloseButtonText))]
    private string? _statusMessage;
    [ObservableProperty]
    private string? _filterText = "";
    private bool _isInitialized;

    public FileAssociationsViewModel(IPicaFileAssociationService service, IViewModelErrorHandler errorHandler,
        string closeButtonText = DesktopUiStrings.FileAssociationsLater)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
        _initialCloseButtonText = closeButtonText ?? throw new ArgumentNullException(nameof(closeButtonText));
        Formats = Array.AsReadOnly(service.SupportedExtensions
            .Select(extension => new FileAssociationFormatViewModel(extension, OnSelectionChanged)).ToArray());
    }

    internal void CancelPendingOperations()
    {
        LoadCommand.Cancel();
        ApplyCommand.Cancel();
    }

    private bool CanChangeSelection()
    {
        return !IsLoading;
    }

    private bool CanApply()
    {
        return _isInitialized && !IsLoading;
    }

    [RelayCommand]
    private async Task LoadAsync(CancellationToken ct)
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            await _service.RegisterAsync(ct);
            IReadOnlyList<string> selected = await _service.LoadSelectionAsync(ct);

            foreach (FileAssociationFormatViewModel format in Formats)
            {
                format.IsSelected = selected.Contains(format.Extension, StringComparer.OrdinalIgnoreCase);
            }

            _isInitialized = true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            ReportError(ex, nameof(LoadAsync));
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanApply))]
    private async Task ApplyAsync(CancellationToken ct)
    {
        IsLoading = true;
        ErrorMessage = null;
        StatusMessage = null;
        string[] selection = Formats.Where(format => format.IsSelected).Select(format => format.Extension).ToArray();

        try
        {
            await _service.ApplyAsync(selection, ct);
            StatusMessage = DesktopUiStrings.FileAssociationsApplied;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            ReportError(ex, nameof(ApplyAsync));
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanChangeSelection))]
    private void SelectAll()
    {
        foreach (FileAssociationFormatViewModel format in Formats)
        {
            format.IsSelected = true;
        }
    }

    [RelayCommand(CanExecute = nameof(CanChangeSelection))]
    private void ClearSelection()
    {
        foreach (FileAssociationFormatViewModel format in Formats)
        {
            format.IsSelected = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanChangeSelection))]
    private void Close()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ReportError(Exception exception, string operation)
    {
        _errorHandler.Log(exception, operation);
        ErrorMessage = exception switch
        {
            NotSupportedException => DesktopUiStrings.FileAssociationsUnsupported,
            AggregateException => DesktopUiStrings.FileAssociationsRestoreFailed,
            _ => DesktopUiStrings.FileAssociationsFailed
        };
    }

    private void OnSelectionChanged()
    {
        StatusMessage = null;
        OnPropertyChanged(nameof(SelectionSummary));
    }

    partial void OnFilterTextChanged(string? value)
    {
        foreach (FileAssociationFormatViewModel format in Formats)
        {
            format.ApplyFilter(value?.Trim() ?? "");
        }

        OnPropertyChanged(nameof(HasMatchingFormats));
    }
}
