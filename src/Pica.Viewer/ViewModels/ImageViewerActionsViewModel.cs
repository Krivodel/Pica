using Microsoft.Extensions.Logging;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Pica.Protocol;
using Pica.Viewer.Services;

namespace Pica.Viewer.ViewModels;

internal sealed partial class ImageViewerActionsViewModel :
    ObservableObject,
    IDisposable
{
    public bool WasSelectionSaved { get; private set; }
    public bool HasErrorMessage => !string.IsNullOrWhiteSpace(ErrorMessage);

    private readonly IViewerImageCommandService _imageCommands;
    private readonly IImagePresentationInfo _presentation;
    private readonly ImageViewerSession _session;
    private readonly IPlatformFileActions _platformFileActions;
    private readonly IViewModelErrorHandler _errorHandler;
    private readonly ILogger<ImageViewerActionsViewModel> _logger;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CopyCurrentCommand))]
    [NotifyCanExecuteChangedFor(nameof(DispatchCurrentCommand))]
    [NotifyCanExecuteChangedFor(nameof(CopySelectionCommand))]
    [NotifyCanExecuteChangedFor(nameof(DispatchSelectionCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveCurrentCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveSelectionCommand))]
    [NotifyCanExecuteChangedFor(nameof(RevealInFolderCommand))]
    private bool _isLoading;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrorMessage))]
    private string? _errorMessage;
    private int _activeOperationCount;
    private int _selectionSaveNotification;

    internal ImageViewerActionsViewModel(
        IViewerImageCommandService imageCommands,
        IImagePresentationInfo presentation,
        ImageViewerSession session,
        IPlatformFileActions platformFileActions,
        IViewModelErrorHandler errorHandler,
        ILogger<ImageViewerActionsViewModel> logger)
    {
        _imageCommands = imageCommands
            ?? throw new ArgumentNullException(nameof(imageCommands));
        _presentation = presentation
            ?? throw new ArgumentNullException(nameof(presentation));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _platformFileActions = platformFileActions
            ?? throw new ArgumentNullException(nameof(platformFileActions));
        _errorHandler = errorHandler
            ?? throw new ArgumentNullException(nameof(errorHandler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _imageCommands.PreparedSelectionSaved += OnPreparedSelectionSaved;
    }

    public void Dispose()
    {
        _imageCommands.PreparedSelectionSaved -= OnPreparedSelectionSaved;
    }

    internal async Task<PreparedClipboardImage?> PrepareSelectionImageAsync(
        ImagePixelSelection selection,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(selection);
        BeginOperation();

        try
        {
            ErrorMessage = null;

            return await _imageCommands.PrepareSelectionAsync(
                selection,
                ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            ErrorMessage = null;
            return null;
        }
        catch (Exception ex)
        {
            _errorHandler.Log(
                ex,
                nameof(PrepareSelectionImageAsync));
            ErrorMessage = _errorHandler.GetUserMessage(ex);

            return null;
        }
        finally
        {
            EndOperation();
        }
    }

    [RelayCommand(CanExecute = nameof(CanExecuteAction))]
    private async Task CopyCurrentAsync(CancellationToken ct)
    {
        await ExecuteActionAsync(
            async operationCt =>
            {
                await _imageCommands.CopyCurrentAsync(operationCt);
                PicaImageItem? item = _presentation.CurrentItem;

                if (item is not null)
                {
                    _logger.LogInformation(
                        "Copied Pica image {ItemId} with channel {Channel} to the clipboard",
                        item.Id,
                        _session.SelectedChannel?.Code);
                }
            },
            nameof(CopyCurrentAsync),
            ct);
    }

    [RelayCommand(CanExecute = nameof(CanExecuteAction))]
    private async Task DispatchCurrentAsync(
        PicaActionDefinition action,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(action);
        await ExecuteActionAsync(
            async operationCt =>
            {
                await _imageCommands.DispatchCurrentAsync(
                    action,
                    operationCt);
                PicaImageItem? item = _presentation.CurrentItem;

                if (item is not null)
                {
                    _logger.LogInformation(
                        "Dispatched Pica action {ActionId} for image {ItemId}",
                        action.Id,
                        item.Id);
                }
            },
            nameof(DispatchCurrentAsync),
            ct);
    }

    [RelayCommand(CanExecute = nameof(CanExecuteAction))]
    private async Task CopySelectionAsync(
        PreparedClipboardImage image,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(image);
        await ExecuteActionAsync(
            async operationCt =>
            {
                await _imageCommands.CopyPreparedImageAsync(
                    image,
                    operationCt);
                _logger.LogInformation(
                    "Copied Pica image selection with {ByteCount} encoded bytes to the clipboard",
                    image.PngContent.Length);
            },
            nameof(CopySelectionAsync),
            ct);
    }

    [RelayCommand(CanExecute = nameof(CanExecuteAction))]
    private async Task DispatchSelectionAsync(
        PreparedSelectionAction selectionAction,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(selectionAction);
        await ExecuteActionAsync(
            async operationCt =>
            {
                await _imageCommands.DispatchPreparedSelectionAsync(
                    selectionAction.Action,
                    selectionAction.Item,
                    selectionAction.Image,
                    operationCt);
                _logger.LogInformation(
                    "Dispatched Pica selection action {ActionId} for image {ItemId}",
                    selectionAction.Action.Id,
                    selectionAction.Item.Id);
            },
            nameof(DispatchSelectionAsync),
            ct);
    }

    [RelayCommand(CanExecute = nameof(CanExecuteAction))]
    private async Task SaveCurrentAsync(CancellationToken ct)
    {
        await ExecuteActionAsync(
            async operationCt =>
            {
                await _imageCommands.SaveCurrentAsync(operationCt);
                PicaImageItem? item = _presentation.CurrentItem;

                if (item is not null)
                {
                    _logger.LogInformation(
                        "Completed save-as for Pica image {ItemId} with channel {Channel}",
                        item.Id,
                        _session.SelectedChannel?.Code);
                }
            },
            nameof(SaveCurrentAsync),
            ct);
    }

    [RelayCommand(CanExecute = nameof(CanExecuteAction))]
    private async Task SaveSelectionAsync(
        PreparedClipboardImage image,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(image);
        Interlocked.Exchange(ref _selectionSaveNotification, 0);
        WasSelectionSaved = false;
        OnPropertyChanged(nameof(WasSelectionSaved));
        await ExecuteActionAsync(
            async operationCt =>
            {
                await _imageCommands.SavePreparedSelectionAsync(
                    image,
                    operationCt);

                bool wasSaved = Interlocked.Exchange(
                    ref _selectionSaveNotification,
                    0) == 1;

                if (!wasSaved)
                {
                    return;
                }

                WasSelectionSaved = true;
                OnPropertyChanged(nameof(WasSelectionSaved));
                _logger.LogInformation(
                    "Completed save-as for a Pica selection sized {Width}x{Height}",
                    image.Dimensions.Width,
                    image.Dimensions.Height);
            },
            nameof(SaveSelectionAsync),
            ct);
    }

    [RelayCommand(CanExecute = nameof(CanExecuteAction))]
    private async Task RevealInFolderAsync(
        FileRevealWindowMode windowMode,
        CancellationToken ct)
    {
        PicaImageItem? item = _presentation.CurrentItem;

        if (item is null)
        {
            return;
        }

        await ExecuteActionAsync(
            async operationCt =>
            {
                await _platformFileActions.RevealInFolderAsync(
                    item.FilePath,
                    windowMode,
                    operationCt);
                LogCompletedPlatformAction("Reveal in folder");
            },
            nameof(RevealInFolderAsync),
            ct);
    }

    private bool CanExecuteAction()
    {
        return !IsLoading;
    }

    private async Task ExecuteActionAsync(
        Func<CancellationToken, Task> action,
        string operationName,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        BeginOperation();

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
            EndOperation();
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

    private void LogCompletedPlatformAction(string operationName)
    {
        _logger.LogInformation(
            "Completed Pica system action {OperationName}",
            operationName);
    }

    private void OnPreparedSelectionSaved(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        Interlocked.Exchange(ref _selectionSaveNotification, 1);
    }
}
