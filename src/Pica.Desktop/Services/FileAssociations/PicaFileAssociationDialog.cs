using Avalonia.Controls;

using Pica.Desktop.Resources;
using Pica.Desktop.ViewModels;
using Pica.Desktop.Views;
using Pica.Viewer.ViewModels;

namespace Pica.Desktop.Services.FileAssociations;

internal sealed class PicaFileAssociationDialog
{
    internal Task Completion => _operation ?? Task.CompletedTask;

    private readonly IPicaFileAssociationService _service;
    private readonly IPicaDesktopStateService _stateService;
    private readonly IViewModelErrorHandler _errorHandler;
    private Task? _operation;

    public PicaFileAssociationDialog(IPicaFileAssociationService service, IPicaDesktopStateService stateService,
        IViewModelErrorHandler errorHandler)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _stateService = stateService ?? throw new ArgumentNullException(nameof(stateService));
        _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
    }

    public Task ShowAsync(Window owner, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(owner);

        return ShowAsync(owner, DesktopUiStrings.Close, ct);
    }

    internal async Task ShowFirstRunAsync(Window owner, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(owner);
        PicaDesktopState state = await _stateService.LoadAsync(ct);

        if (!state.HasSeenFileAssociationsPrompt && owner.IsVisible)
        {
            await ShowAsync(owner, DesktopUiStrings.FileAssociationsLater, ct);
        }
    }

    private async Task ShowAsync(Window owner, string closeButtonText, CancellationToken ct)
    {
        Task operation = ShowCoreAsync(owner, closeButtonText, ct);
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

    private async Task ShowCoreAsync(Window owner, string closeButtonText, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        FileAssociationsViewModel viewModel = new(_service, _errorHandler, closeButtonText);
        FileAssociationsWindow window = new(viewModel);

        if (owner.Screens.ScreenFromWindow(owner) is { } screen)
        {
            double availableWidth = screen.WorkingArea.Width / screen.Scaling;
            double availableHeight = screen.WorkingArea.Height / screen.Scaling;
            window.MinWidth = Math.Min(window.MinWidth, availableWidth);
            window.MinHeight = Math.Min(window.MinHeight, availableHeight);
            window.MaxWidth = availableWidth;
            window.MaxHeight = availableHeight;
            window.Width = Math.Min(window.Width, availableWidth);
            window.Height = Math.Min(window.Height, availableHeight);
        }

        using CancellationTokenRegistration cancellation = ct.Register(() => window.Dispatcher.Post(window.Close));
        Task dialog = window.ShowDialog(owner);

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

            if (window.IsVisible)
            {
                window.Close();
            }
        }

        ct.ThrowIfCancellationRequested();
        await _service.DismissPromptAsync(ct);
    }
}
