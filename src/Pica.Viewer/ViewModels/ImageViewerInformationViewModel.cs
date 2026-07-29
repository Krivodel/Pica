using System.ComponentModel;

using CommunityToolkit.Mvvm.ComponentModel;

using Pica.Protocol;
using Pica.Viewer.Helpers;
using Pica.Viewer.Services;

namespace Pica.Viewer.ViewModels;

internal sealed partial class ImageViewerInformationViewModel :
    ObservableObject,
    IDisposable
{
    public bool HasErrorMessage => !string.IsNullOrWhiteSpace(ErrorMessage);

    private readonly ImageViewerSession _session;
    private readonly IImagePresentationInfo _presentation;
    private readonly IImageFileMetadataProvider _metadataProvider;
    private readonly ImageViewerSettingsViewModel _settings;
    private readonly IViewModelErrorHandler _errorHandler;
    [ObservableProperty]
    private bool _isLoading;
    [ObservableProperty]
    private string _information = string.Empty;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrorMessage))]
    private string? _errorMessage;
    private OperationCancellation? _metadataLoadCancellation;
    private Task? _metadataLoadTask;
    private Guid? _metadataItemId;
    private string? _metadataFilePath;
    private DateTime? _modificationDate;
    private bool _hasLoadedMetadata;
    private bool _isStarted;
    private bool _disposed;

    internal ImageViewerInformationViewModel(
        ImageViewerSession session,
        IImagePresentationInfo presentation,
        IImageFileMetadataProvider metadataProvider,
        ImageViewerSettingsViewModel settings,
        IViewModelErrorHandler errorHandler)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _presentation = presentation
            ?? throw new ArgumentNullException(nameof(presentation));
        _metadataProvider = metadataProvider
            ?? throw new ArgumentNullException(nameof(metadataProvider));
        _settings = settings
            ?? throw new ArgumentNullException(nameof(settings));
        _errorHandler = errorHandler
            ?? throw new ArgumentNullException(nameof(errorHandler));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_isStarted)
        {
            _session.PropertyChanged -= OnSessionPropertyChanged;
            _presentation.Changed -= OnPresentationChanged;
            _settings.PropertyChanged -= OnSettingsPropertyChanged;
        }

        CancelMetadataLoad();
        _disposed = true;
    }

    internal void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_isStarted)
        {
            return;
        }

        _isStarted = true;
        _session.PropertyChanged += OnSessionPropertyChanged;
        _presentation.Changed += OnPresentationChanged;
        _settings.PropertyChanged += OnSettingsPropertyChanged;
        UpdateInformation(refreshMetadata: true);
    }

    private DateTime? GetLoadedModificationDate(PicaImageItem item)
    {
        return _hasLoadedMetadata
            && (_metadataItemId == item.Id)
            && string.Equals(
                _metadataFilePath,
                item.FilePath,
                StringComparison.OrdinalIgnoreCase)
                ? _modificationDate
                : null;
    }

    private void UpdateInformation(bool refreshMetadata)
    {
        PicaImageItem? selectedItem = _session.SelectedItem;

        if (selectedItem is null)
        {
            CancelMetadataLoad();
            Information = string.Empty;
            return;
        }

        PicaImageItem informationItem = selectedItem;
        ImageDimensions dimensions = new();
        PicaImageItem? currentItem = _presentation.CurrentItem;

        if ((currentItem is not null)
            && (currentItem.Id == selectedItem.Id))
        {
            informationItem = currentItem;
            dimensions = _presentation.SourceDimensions;
        }

        ImageViewerInformationOptions options =
            _settings.CreateInformationOptions();

        if (options.ShowModificationDate && refreshMetadata)
        {
            StartMetadataLoad(informationItem);
        }
        else if (!options.ShowModificationDate)
        {
            CancelMetadataLoad();
            ClearLoadedMetadata();
        }

        DateTime? modificationDate = options.ShowModificationDate
            ? GetLoadedModificationDate(informationItem)
            : null;
        Information = ImageViewerInformationFormatter.Format(
            informationItem.FileName,
            dimensions,
            _session.SelectedChannel,
            modificationDate,
            options);
    }

    private void StartMetadataLoad(PicaImageItem item)
    {
        if ((_metadataLoadCancellation is not null)
            && (_metadataItemId == item.Id)
            && string.Equals(
                _metadataFilePath,
                item.FilePath,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        CancelMetadataLoad();
        OperationCancellation cancellation = new();
        _metadataLoadCancellation = cancellation;
        _metadataItemId = item.Id;
        _metadataFilePath = item.FilePath;
        _hasLoadedMetadata = false;
        IsLoading = true;
        ErrorMessage = null;
        Task loadTask = LoadMetadataAsync(
            item.Id,
            item.FilePath,
            cancellation);
        _metadataLoadTask = loadTask.IsCompleted ? null : loadTask;
    }

    private async Task LoadMetadataAsync(
        Guid itemId,
        string filePath,
        OperationCancellation cancellation)
    {
        try
        {
            DateTime? modificationDate = await _metadataProvider
                .GetModificationDateAsync(
                    filePath,
                    cancellation.Token);

            if (!CanApplyMetadata(itemId, filePath, cancellation))
            {
                return;
            }

            _modificationDate = modificationDate;
            _hasLoadedMetadata = true;
            UpdateInformation(refreshMetadata: false);
        }
        catch (OperationCanceledException)
            when (cancellation.IsCancellationRequested)
        {
            if (object.ReferenceEquals(
                _metadataLoadCancellation,
                cancellation))
            {
                ErrorMessage = null;
            }
        }
        catch (Exception ex)
        {
            if (object.ReferenceEquals(
                _metadataLoadCancellation,
                cancellation))
            {
                _errorHandler.Log(ex, nameof(LoadMetadataAsync));
                ErrorMessage = _errorHandler.GetUserMessage(ex);
            }
        }
        finally
        {
            if (object.ReferenceEquals(
                _metadataLoadCancellation,
                cancellation))
            {
                _metadataLoadCancellation = null;
                _metadataLoadTask = null;
                IsLoading = false;
            }

            cancellation.Complete();
        }
    }

    private bool CanApplyMetadata(
        Guid itemId,
        string filePath,
        OperationCancellation cancellation)
    {
        PicaImageItem? selectedItem = _session.SelectedItem;

        return !cancellation.IsCancellationRequested
            && !_disposed
            && object.ReferenceEquals(
                _metadataLoadCancellation,
                cancellation)
            && (selectedItem?.Id == itemId)
            && string.Equals(
                selectedItem.FilePath,
                filePath,
                StringComparison.OrdinalIgnoreCase);
    }

    private void CancelMetadataLoad()
    {
        OperationCancellation? cancellation =
            _metadataLoadCancellation;
        _metadataLoadCancellation = null;
        _metadataLoadTask = null;
        cancellation?.Cancel();
        IsLoading = false;
    }

    private void ClearLoadedMetadata()
    {
        _metadataItemId = null;
        _metadataFilePath = null;
        _modificationDate = null;
        _hasLoadedMetadata = false;
    }

    private void OnSessionPropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        _ = sender;

        if (string.Equals(
                e.PropertyName,
                nameof(ImageViewerSession.SelectedIndex),
                StringComparison.Ordinal)
            || string.Equals(
                e.PropertyName,
                nameof(ImageViewerSession.SelectedChannel),
                StringComparison.Ordinal))
        {
            UpdateInformation(refreshMetadata: true);
        }
    }

    private void OnPresentationChanged(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        UpdateInformation(refreshMetadata: true);
    }

    private void OnSettingsPropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        _ = sender;

        if (string.Equals(
                e.PropertyName,
                nameof(ImageViewerSettingsViewModel.ShowImageName),
                StringComparison.Ordinal)
            || string.Equals(
                e.PropertyName,
                nameof(ImageViewerSettingsViewModel.ShowImageFormat),
                StringComparison.Ordinal)
            || string.Equals(
                e.PropertyName,
                nameof(ImageViewerSettingsViewModel.ShowImageResolution),
                StringComparison.Ordinal)
            || string.Equals(
                e.PropertyName,
                nameof(ImageViewerSettingsViewModel.ShowImageModificationDate),
                StringComparison.Ordinal))
        {
            UpdateInformation(refreshMetadata: true);
        }
    }
}
