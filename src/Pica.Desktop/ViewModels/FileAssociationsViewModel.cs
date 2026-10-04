using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Krivodeling.Localization.Avalonia;
using Pica.Desktop.Resources;
using Pica.Desktop.Services.FileAssociations;
using Pica.Viewer.Services;
using Pica.Viewer.ViewModels;

namespace Pica.Desktop.ViewModels;

internal sealed partial class FileAssociationsViewModel : ObservableObject
{
    public ReadOnlyCollection<FileAssociationFormatViewModel> Formats { get; }
    public ReadOnlyCollection<FileAssociationFormatGroupViewModel> FormatGroups { get; }
    public string SelectionSummary => string.Format(
        DesktopLocalization.Get(PicaDesktopLocalizationKeys.FileAssociationsSelectionSummaryFormat),
        Formats.Count(format => format.IsSelected), Formats.Count);
    public bool HasErrorMessage => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool HasMatchingFormats => Formats.Any(format => format.MatchesFilter);
    public string CloseButtonText => _closeButtonLocalizationKey is { } key
        ? LocalizationText.Get(key) : _closeButtonText;

    public event EventHandler? CloseRequested;

    private readonly string _closeButtonText;
    private readonly string? _closeButtonLocalizationKey;
    private readonly IPicaFileAssociationService _service;
    private readonly IViewModelErrorHandler _errorHandler;
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
    private string? _filterText = "";
    private bool _isInitialized;

    public FileAssociationsViewModel(IPicaFileAssociationService service, IViewModelErrorHandler errorHandler,
        IImageFormatRegistry formatRegistry, string? closeButtonText = null, string? closeButtonLocalizationKey = null)
    {
        ArgumentNullException.ThrowIfNull(formatRegistry);
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
        _closeButtonText = closeButtonText ?? DesktopLocalization.Get(PicaDesktopLocalizationKeys.FileAssociationsLater);
        _closeButtonLocalizationKey = closeButtonLocalizationKey
            ?? (closeButtonText is null ? PicaDesktopLocalizationKeys.FileAssociationsLater : null);
        Formats = Array.AsReadOnly(service.SupportedExtensions
            .Select(extension => new FileAssociationFormatViewModel(extension, OnSelectionChanged)).ToArray());
        FormatGroups = Array.AsReadOnly(Formats
            .GroupBy(format =>
            {
                string contentType = formatRegistry.GetContentType(format.Extension);

                return string.Equals(contentType, PicaImageFormats.HeicContentType, StringComparison.OrdinalIgnoreCase)
                    ? PicaImageFormats.HeifContentType : contentType;
            }, StringComparer.OrdinalIgnoreCase)
            .Select(group => new FileAssociationFormatGroupViewModel(Array.AsReadOnly(group
                .OrderBy(format => format.Extension, StringComparer.OrdinalIgnoreCase).ToArray())))
            .OrderBy(group => group.Formats[0].Extension, StringComparer.OrdinalIgnoreCase)
            .ToArray());
    }

    internal void RefreshLocalization()
    {
        OnPropertyChanged(nameof(SelectionSummary));
        OnPropertyChanged(nameof(CloseButtonText));
    }

    internal void CancelPendingOperations()
    {
        LoadCommand.Cancel();
        ApplyCommand.Cancel();
        CloseCommand.Cancel();
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
        string[] selection = Formats.Where(format => format.IsSelected).Select(format => format.Extension).ToArray();
        bool isApplied = false;

        try
        {
            await _service.ApplyAsync(selection, ct);
            ct.ThrowIfCancellationRequested();
            isApplied = true;
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

        if (isApplied)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
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
    private async Task CloseAsync(CancellationToken ct)
    {
        IsLoading = true;
        ErrorMessage = null;
        bool isSaved = false;

        try
        {
            await _service.DismissPromptAsync(ct);
            isSaved = true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _errorHandler.Log(ex, nameof(CloseAsync));
            ErrorMessage = DesktopLocalization.Get(PicaDesktopLocalizationKeys.PreferenceChoiceFailed);
        }
        finally
        {
            IsLoading = false;
        }

        if (isSaved)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ReportError(Exception exception, string operation)
    {
        _errorHandler.Log(exception, operation);
        ErrorMessage = exception switch
        {
            NotSupportedException => DesktopLocalization.Get(PicaDesktopLocalizationKeys.FileAssociationsUnsupported),
            AggregateException => DesktopLocalization.Get(PicaDesktopLocalizationKeys.FileAssociationsRestoreFailed),
            _ => DesktopLocalization.Get(PicaDesktopLocalizationKeys.FileAssociationsFailed)
        };
    }

    private void OnSelectionChanged()
    {
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
