using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Pica.Desktop.Resources;
using Pica.Desktop.Services;
using Pica.Viewer.ViewModels;

namespace Pica.Desktop.ViewModels;

internal sealed partial class ClipboardShortcutOfferViewModel : ObservableObject
{
    public bool HasErrorMessage => !string.IsNullOrWhiteSpace(ErrorMessage);

    public event EventHandler? CloseRequested;

    private readonly Func<CancellationToken, Task> _enableAsync;
    private readonly Func<CancellationToken, Task> _dismissAsync;
    private readonly IViewModelErrorHandler _errorHandler;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EnableCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeclineCommand))]
    private bool _isLoading;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EnableCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeclineCommand))]
    private bool _isRecording;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrorMessage))]
    private string? _errorMessage;

    public ClipboardShortcutOfferViewModel(Func<CancellationToken, Task> enableAsync,
        Func<CancellationToken, Task> dismissAsync, IViewModelErrorHandler errorHandler)
    {
        _enableAsync = enableAsync ?? throw new ArgumentNullException(nameof(enableAsync));
        _dismissAsync = dismissAsync ?? throw new ArgumentNullException(nameof(dismissAsync));
        _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
    }

    private bool CanChoose()
    {
        return !IsLoading && !IsRecording;
    }

    [RelayCommand(CanExecute = nameof(CanChoose))]
    private async Task EnableAsync(CancellationToken ct)
    {
        await ChooseAsync(_enableAsync, ct);
    }

    [RelayCommand(CanExecute = nameof(CanChoose))]
    private async Task DeclineAsync(CancellationToken ct)
    {
        await ChooseAsync(_dismissAsync, ct);
    }

    private async Task ChooseAsync(Func<CancellationToken, Task> chooseAsync, CancellationToken ct)
    {
        IsLoading = true;
        ErrorMessage = null;
        bool isSaved = false;

        try
        {
            await chooseAsync(ct);
            isSaved = true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _errorHandler.Log(ex, nameof(ChooseAsync));
            ErrorMessage = ex is PicaShortcutException
                ? PicaClipboardShortcutSettingContributionProvider.GetErrorMessage(ex)
                : DesktopUiStrings.PreferenceChoiceFailed;
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
}
