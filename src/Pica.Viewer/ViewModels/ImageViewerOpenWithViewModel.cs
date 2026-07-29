using Microsoft.Extensions.Logging;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Pica.Viewer.Services;

namespace Pica.Viewer.ViewModels;

internal sealed partial class ImageViewerOpenWithViewModel :
    ObservableObject
{
    public bool IsSupported => _platformFileActions.SupportsOpenWith;
    public IReadOnlyList<OpenWithApplication> Applications =>
        _applications;
    public bool HasLoadedApplications { get; private set; }
    public OpenWithTarget? LoadedTarget { get; private set; }
    public bool IsPrepared { get; private set; }
    public bool HasErrorMessage => !string.IsNullOrWhiteSpace(ErrorMessage);

    private readonly IViewerImageCommandService _imageCommands;
    private readonly IPlatformFileActions _platformFileActions;
    private readonly IViewModelErrorHandler _errorHandler;
    private readonly ILogger<ImageViewerOpenWithViewModel> _logger;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadApplicationsCommand))]
    [NotifyCanExecuteChangedFor(nameof(PrepareCurrentImageCommand))]
    [NotifyCanExecuteChangedFor(nameof(PrepareSelectionCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenWithApplicationCommand))]
    [NotifyCanExecuteChangedFor(nameof(ChooseApplicationCommand))]
    private bool _isLoading;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrorMessage))]
    private string? _errorMessage;
    private IReadOnlyList<OpenWithApplication> _applications =
        new List<OpenWithApplication>();

    internal ImageViewerOpenWithViewModel(
        IViewerImageCommandService imageCommands,
        IPlatformFileActions platformFileActions,
        IViewModelErrorHandler errorHandler,
        ILogger<ImageViewerOpenWithViewModel> logger)
    {
        _imageCommands = imageCommands
            ?? throw new ArgumentNullException(nameof(imageCommands));
        _platformFileActions = platformFileActions
            ?? throw new ArgumentNullException(nameof(platformFileActions));
        _errorHandler = errorHandler
            ?? throw new ArgumentNullException(nameof(errorHandler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [RelayCommand(CanExecute = nameof(CanExecuteAction))]
    private async Task LoadApplicationsAsync(
        OpenWithTarget target,
        CancellationToken ct)
    {
        HasLoadedApplications = false;
        LoadedTarget = null;
        OnPropertyChanged(nameof(HasLoadedApplications));
        OnPropertyChanged(nameof(LoadedTarget));
        await ExecuteActionAsync(
            async operationCt =>
            {
                string associationFilePath =
                    GetAssociationFilePath(target);
                _applications =
                    await _platformFileActions
                        .GetOpenWithApplicationsAsync(
                            associationFilePath,
                            operationCt);
                OnPropertyChanged(nameof(Applications));
                HasLoadedApplications = true;
                LoadedTarget = target;
                OnPropertyChanged(nameof(HasLoadedApplications));
                OnPropertyChanged(nameof(LoadedTarget));
                _logger.LogDebug(
                    "Loaded {ApplicationCount} applications for Pica open-with target {Target}",
                    _applications.Count,
                    target);
            },
            nameof(LoadApplicationsAsync),
            ct);
    }

    [RelayCommand(CanExecute = nameof(CanExecuteAction))]
    private async Task PrepareCurrentImageAsync(CancellationToken ct)
    {
        ResetPreparation();
        await ExecuteActionAsync(
            async operationCt =>
            {
                await _imageCommands.PrepareCurrentOpenWithFileAsync(
                    operationCt);
                UpdatePreparation();
            },
            nameof(PrepareCurrentImageAsync),
            ct);
    }

    [RelayCommand(CanExecute = nameof(CanExecuteAction))]
    private async Task PrepareSelectionAsync(
        PreparedClipboardImage image,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(image);
        ResetPreparation();
        await ExecuteActionAsync(
            async operationCt =>
            {
                await _imageCommands.PrepareSelectionOpenWithFileAsync(
                    image,
                    operationCt);
                UpdatePreparation();
            },
            nameof(PrepareSelectionAsync),
            ct);
    }

    [RelayCommand(CanExecute = nameof(CanExecuteAction))]
    private async Task OpenWithApplicationAsync(
        OpenWithApplication application,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(application);
        string? filePath = _imageCommands.PreparedOpenWithFilePath;

        if (filePath is null)
        {
            return;
        }

        await ExecuteActionAsync(
            async operationCt =>
            {
                await _platformFileActions.OpenWithAsync(
                    filePath,
                    application,
                    operationCt);
                LogCompletedPlatformAction("Open with");
            },
            nameof(OpenWithApplicationAsync),
            ct);
    }

    [RelayCommand(CanExecute = nameof(CanExecuteAction))]
    private async Task ChooseApplicationAsync(CancellationToken ct)
    {
        string? filePath = _imageCommands.PreparedOpenWithFilePath;

        if (filePath is null)
        {
            return;
        }

        await ExecuteActionAsync(
            async operationCt =>
            {
                await _platformFileActions.ChooseApplicationAsync(
                    filePath,
                    operationCt);
                LogCompletedPlatformAction("Choose application");
            },
            nameof(ChooseApplicationAsync),
            ct);
    }

    private bool CanExecuteAction()
    {
        return !IsLoading;
    }

    private string GetAssociationFilePath(OpenWithTarget target)
    {
        return target == OpenWithTarget.Selection
            ? PicaImageFormats.SelectionFileName
            : _imageCommands.GetCurrentOpenWithAssociationFilePath();
    }

    private async Task ExecuteActionAsync(
        Func<CancellationToken, Task> action,
        string operationName,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        IsLoading = true;

        try
        {
            ErrorMessage = null;
            await action(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            _errorHandler.Log(ex, operationName);
            ErrorMessage = _errorHandler.GetUserMessage(ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ResetPreparation()
    {
        IsPrepared = false;
        OnPropertyChanged(nameof(IsPrepared));
    }

    private void UpdatePreparation()
    {
        IsPrepared =
            _imageCommands.PreparedOpenWithFilePath is not null;
        OnPropertyChanged(nameof(IsPrepared));
    }

    private void LogCompletedPlatformAction(string operationName)
    {
        _logger.LogInformation(
            "Completed Pica system action {OperationName}",
            operationName);
    }
}
