using Avalonia.Controls;

using Krivodeling.Localization.Avalonia;
using Pica.Desktop.Resources;
using Pica.Desktop.ViewModels;
using Pica.Desktop.Views;
using Pica.Viewer.Services;
using Pica.Viewer.ViewModels;

namespace Pica.Desktop.Services.FileAssociations;

internal sealed class PicaFileAssociationDialog : IPicaStartupPrompt
{
    public Task Completion => _operation ?? Task.CompletedTask;

    private readonly IPicaFileAssociationService _service;
    private readonly IPicaDesktopStateService _stateService;
    private readonly IViewModelErrorHandler _errorHandler;
    private readonly IImageFormatRegistry _formatRegistry;
    private Task? _operation;

    public PicaFileAssociationDialog(IPicaFileAssociationService service, IPicaDesktopStateService stateService,
        IViewModelErrorHandler errorHandler, IImageFormatRegistry formatRegistry)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _stateService = stateService ?? throw new ArgumentNullException(nameof(stateService));
        _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
        _formatRegistry = formatRegistry ?? throw new ArgumentNullException(nameof(formatRegistry));
    }

    public Task ShowAsync(Window owner, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(owner);

        return ShowAsync(owner, PicaDesktopLocalizationKeys.Cancel, ct);
    }

    public async Task ShowIfNeededAsync(Window owner, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(owner);
        PicaDesktopState state = await _stateService.LoadAsync(ct);

        if (!state.HasSeenFileAssociationsPrompt && owner.IsVisible)
        {
            await ShowAsync(owner, PicaDesktopLocalizationKeys.FileAssociationsLater, ct);
        }
    }

    private async Task ShowAsync(Window owner, string closeButtonKey, CancellationToken ct)
    {
        Task operation = ShowCoreAsync(owner, closeButtonKey, ct);
        _operation = operation;

        try
        {
            await operation;
        }
        finally
        {
            _operation = null;
        }
    }

    private async Task ShowCoreAsync(Window owner, string closeButtonKey, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        FileAssociationsViewModel viewModel = new(_service, _errorHandler, _formatRegistry,
            LocalizationText.Get(closeButtonKey), closeButtonKey);
        FileAssociationsWindow window = new(viewModel);

        Task dialog = DesktopDialogPresenter.ShowAsync(window, owner, ct);

        try
        {
            await viewModel.LoadCommand.ExecuteAsync(null);
            await dialog;
        }
        finally
        {
            viewModel.CancelPendingOperations();

            if (viewModel.ApplyCommand.ExecutionTask is { } apply)
            {
                await apply;
            }

            if (viewModel.CloseCommand.ExecutionTask is { } closing)
            {
                await closing;
            }

            if (window.IsVisible)
            {
                window.Close();
            }
        }

        ct.ThrowIfCancellationRequested();
        await _service.DismissPromptAsync(ct);
    }
}
