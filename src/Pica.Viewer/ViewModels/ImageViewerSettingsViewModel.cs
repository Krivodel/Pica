using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Pica.Viewer.Helpers;
using Pica.Viewer.Services;

namespace Pica.Viewer.ViewModels;

internal sealed partial class ImageViewerSettingsViewModel :
    ObservableObject,
    IDisposable
{
    public int BackgroundIdleTimeoutSeconds =>
        _state.BackgroundIdleTimeoutSeconds;
    public bool IsCheckerboardBackgroundEnabled =>
        _state.IsCheckerboardBackgroundEnabled;
    public bool IsFilteringEnabled => _state.IsFilteringEnabled;
    public int MovementSpeed => _state.MovementSpeed;
    public int ZoomSpeed => _state.ZoomSpeed;
    public bool ExpandOnDoubleClick => _state.ExpandOnDoubleClick;
    public bool IsFastLoadingEnabled => _state.IsFastLoadingEnabled;
    public bool AllowFreeZoomOut => _state.AllowFreeZoomOut;
    public bool IsPanningInertiaEnabled => _state.IsPanningInertiaEnabled;
    public WindowResizeBehavior ResizeBehavior => _state.ResizeBehavior;
    public bool RememberWindowPlacement => _state.RememberWindowPlacement;
    public bool ShowImageName => _state.ShowImageName;
    public bool ShowImageFormat => _state.ShowImageFormat;
    public bool ShowImageResolution => _state.ShowImageResolution;
    public bool ShowImageModificationDate => _state.ShowImageModificationDate;
    public bool IsWindowed => _state.IsWindowed == true;
    public int? WindowX => _state.WindowX;
    public int? WindowY => _state.WindowY;
    public double? WindowWidth => _state.WindowWidth;
    public double? WindowHeight => _state.WindowHeight;
    public bool HasErrorMessage => !string.IsNullOrWhiteSpace(ErrorMessage);

    private readonly IImageViewerStateService _stateService;
    private readonly ImageViewerSession _session;
    private readonly IImageLoadingSettings _imageLoadingSettings;
    private readonly IViewerWindowPlacementProvider _windowPlacementProvider;
    private readonly IViewModelErrorHandler _errorHandler;
    private readonly SemaphoreSlim _persistenceLock = new(1, 1);
    [ObservableProperty]
    private bool _isLoading;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrorMessage))]
    private string? _errorMessage;
    private ImageViewerState _state;
    private int _activeOperationCount;

    internal ImageViewerSettingsViewModel(
        IImageViewerStateService stateService,
        ImageViewerSession session,
        IImageLoadingSettings imageLoadingSettings,
        IViewerWindowPlacementProvider windowPlacementProvider,
        IViewModelErrorHandler errorHandler,
        ImageViewerState initialState)
    {
        _stateService = stateService
            ?? throw new ArgumentNullException(nameof(stateService));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _imageLoadingSettings = imageLoadingSettings
            ?? throw new ArgumentNullException(nameof(imageLoadingSettings));
        _windowPlacementProvider = windowPlacementProvider
            ?? throw new ArgumentNullException(nameof(windowPlacementProvider));
        _errorHandler = errorHandler
            ?? throw new ArgumentNullException(nameof(errorHandler));
        _state = initialState?.CreateCopy()
            ?? throw new ArgumentNullException(nameof(initialState));
    }

    public void Dispose()
    {
        _persistenceLock.Dispose();
    }

    internal ImageViewerInformationOptions CreateInformationOptions()
    {
        return new ImageViewerInformationOptions(
            ShowImageName,
            ShowImageFormat,
            ShowImageResolution,
            ShowImageModificationDate);
    }

    [RelayCommand]
    private async Task ChangeBackgroundIdleTimeoutAsync(
        int timeoutSeconds,
        CancellationToken ct)
    {
        await ChangeSettingAsync(
            () =>
            {
                _state.BackgroundIdleTimeoutSeconds = timeoutSeconds;
                OnPropertyChanged(nameof(BackgroundIdleTimeoutSeconds));
            },
            nameof(ChangeBackgroundIdleTimeoutAsync),
            ct);
    }

    [RelayCommand]
    private async Task ToggleCheckerboardBackgroundAsync(
        CancellationToken ct)
    {
        await ChangeSettingAsync(
            () =>
            {
                _state.IsCheckerboardBackgroundEnabled =
                    !_state.IsCheckerboardBackgroundEnabled;
                OnPropertyChanged(
                    nameof(IsCheckerboardBackgroundEnabled));
            },
            nameof(ToggleCheckerboardBackgroundAsync),
            ct);
    }

    [RelayCommand]
    private async Task ToggleFilteringAsync(CancellationToken ct)
    {
        await ChangeSettingAsync(
            () =>
            {
                _session.ToggleFiltering();
                _state.IsFilteringEnabled =
                    _session.IsFilteringEnabled;
                OnPropertyChanged(nameof(IsFilteringEnabled));
            },
            nameof(ToggleFilteringAsync),
            ct);
    }

    [RelayCommand]
    private async Task ChangeMovementSpeedAsync(
        int movementSpeed,
        CancellationToken ct)
    {
        await ChangeSettingAsync(
            () =>
            {
                _state.MovementSpeed = movementSpeed;
                OnPropertyChanged(nameof(MovementSpeed));
            },
            nameof(ChangeMovementSpeedAsync),
            ct);
    }

    [RelayCommand]
    private async Task ChangeZoomSpeedAsync(
        int zoomSpeed,
        CancellationToken ct)
    {
        await ChangeSettingAsync(
            () =>
            {
                _state.ZoomSpeed = zoomSpeed;
                OnPropertyChanged(nameof(ZoomSpeed));
            },
            nameof(ChangeZoomSpeedAsync),
            ct);
    }

    [RelayCommand]
    private async Task ChangeExpandOnDoubleClickAsync(
        bool expandOnDoubleClick,
        CancellationToken ct)
    {
        await ChangeSettingAsync(
            () =>
            {
                _state.ExpandOnDoubleClick = expandOnDoubleClick;
                OnPropertyChanged(nameof(ExpandOnDoubleClick));
            },
            nameof(ChangeExpandOnDoubleClickAsync),
            ct);
    }

    [RelayCommand]
    private async Task ChangeFastLoadingAsync(
        bool isFastLoadingEnabled,
        CancellationToken ct)
    {
        await ChangeSettingAsync(
            () =>
            {
                _state.IsFastLoadingEnabled = isFastLoadingEnabled;
                OnPropertyChanged(nameof(IsFastLoadingEnabled));
                _imageLoadingSettings.SetFastLoadingEnabled(
                    isFastLoadingEnabled);
            },
            nameof(ChangeFastLoadingAsync),
            ct);
    }

    [RelayCommand]
    private async Task ChangeAllowFreeZoomOutAsync(
        bool allowFreeZoomOut,
        CancellationToken ct)
    {
        await ChangeSettingAsync(
            () =>
            {
                _state.AllowFreeZoomOut = allowFreeZoomOut;
                OnPropertyChanged(nameof(AllowFreeZoomOut));
            },
            nameof(ChangeAllowFreeZoomOutAsync),
            ct);
    }

    [RelayCommand]
    private async Task ChangePanningInertiaAsync(
        bool isPanningInertiaEnabled,
        CancellationToken ct)
    {
        await ChangeSettingAsync(
            () =>
            {
                _state.IsPanningInertiaEnabled = isPanningInertiaEnabled;
                OnPropertyChanged(nameof(IsPanningInertiaEnabled));
            },
            nameof(ChangePanningInertiaAsync),
            ct);
    }

    [RelayCommand]
    private async Task ChangeResizeBehaviorAsync(
        WindowResizeBehavior resizeBehavior,
        CancellationToken ct)
    {
        await ChangeSettingAsync(
            () =>
            {
                _state.ResizeBehavior = resizeBehavior;
                OnPropertyChanged(nameof(ResizeBehavior));
            },
            nameof(ChangeResizeBehaviorAsync),
            ct);
    }

    [RelayCommand]
    private async Task ChangeRememberWindowPlacementAsync(
        bool rememberWindowPlacement,
        CancellationToken ct)
    {
        await ChangeSettingAsync(
            () =>
            {
                _state.RememberWindowPlacement = rememberWindowPlacement;
                OnPropertyChanged(nameof(RememberWindowPlacement));
            },
            nameof(ChangeRememberWindowPlacementAsync),
            ct);
    }

    [RelayCommand]
    private async Task ChangeShowImageNameAsync(
        bool showImageName,
        CancellationToken ct)
    {
        await ChangeSettingAsync(
            () =>
            {
                _state.ShowImageName = showImageName;
                OnPropertyChanged(nameof(ShowImageName));
            },
            nameof(ChangeShowImageNameAsync),
            ct);
    }

    [RelayCommand]
    private async Task ChangeShowImageFormatAsync(
        bool showImageFormat,
        CancellationToken ct)
    {
        await ChangeSettingAsync(
            () =>
            {
                _state.ShowImageFormat = showImageFormat;
                OnPropertyChanged(nameof(ShowImageFormat));
            },
            nameof(ChangeShowImageFormatAsync),
            ct);
    }

    [RelayCommand]
    private async Task ChangeShowImageResolutionAsync(
        bool showImageResolution,
        CancellationToken ct)
    {
        await ChangeSettingAsync(
            () =>
            {
                _state.ShowImageResolution = showImageResolution;
                OnPropertyChanged(nameof(ShowImageResolution));
            },
            nameof(ChangeShowImageResolutionAsync),
            ct);
    }

    [RelayCommand]
    private async Task ChangeShowImageModificationDateAsync(
        bool showImageModificationDate,
        CancellationToken ct)
    {
        await ChangeSettingAsync(
            () =>
            {
                _state.ShowImageModificationDate =
                    showImageModificationDate;
                OnPropertyChanged(nameof(ShowImageModificationDate));
            },
            nameof(ChangeShowImageModificationDateAsync),
            ct);
    }

    [RelayCommand]
    private async Task PersistWindowStateAsync(CancellationToken ct)
    {
        await ExecutePersistenceAsync(
            nameof(PersistWindowStateAsync),
            ct);
    }

    private async Task ChangeSettingAsync(
        Action changeSetting,
        string operationName,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(changeSetting);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        BeginOperation();

        try
        {
            ErrorMessage = null;
            changeSetting();
            await SaveCurrentStateAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            HandleError(ex, operationName);
        }
        finally
        {
            EndOperation();
        }
    }

    private async Task ExecutePersistenceAsync(
        string operationName,
        CancellationToken ct)
    {
        BeginOperation();

        try
        {
            ErrorMessage = null;
            await SaveCurrentStateAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            HandleError(ex, operationName);
        }
        finally
        {
            EndOperation();
        }
    }

    private async Task SaveCurrentStateAsync(CancellationToken ct)
    {
        await _persistenceLock.WaitAsync(ct);

        try
        {
            ViewerWindowPlacement placement =
                _windowPlacementProvider.GetCurrentPlacement();
            ImageViewerState snapshot = _state.CreateSnapshot(
                placement.IsWindowed,
                placement.WindowX,
                placement.WindowY,
                placement.WindowWidth,
                placement.WindowHeight);
            await _stateService.SaveAsync(snapshot, ct);
            _state = snapshot;
        }
        finally
        {
            _persistenceLock.Release();
        }
    }

    private void BeginOperation()
    {
        _activeOperationCount++;
        IsLoading = true;
    }

    private void EndOperation()
    {
        _activeOperationCount--;
        IsLoading = _activeOperationCount > 0;
    }

    private void HandleError(Exception exception, string operationName)
    {
        _errorHandler.Log(exception, operationName);
        ErrorMessage = _errorHandler.GetUserMessage(exception);
    }
}
