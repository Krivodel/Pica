using Avalonia.Controls;

using Pica.Desktop.ViewModels;
using Pica.Desktop.Views;
using Pica.Viewer.Services;
using Pica.Viewer.ViewModels;

namespace Pica.Desktop.Services;

internal sealed class PicaClipboardShortcutDialog : IPicaStartupPrompt
{
    public Task Completion => _operation ?? Task.CompletedTask;

    private readonly IPicaDesktopStateService _stateService;
    private readonly Func<CancellationToken, Task<IReadOnlyList<ViewerSettingContribution>>> _createOptionsAsync;
    private readonly Func<CancellationToken, Task> _enableAsync;
    private readonly IViewModelErrorHandler _errorHandler;
    private Task? _operation;

    public PicaClipboardShortcutDialog(IPicaDesktopStateService stateService,
        PicaClipboardShortcutSettingContributionProvider settingsProvider, IViewModelErrorHandler errorHandler)
        : this(stateService,
            (settingsProvider ?? throw new ArgumentNullException(nameof(settingsProvider))).CreateOptionsAsync,
            settingsProvider.EnableAsync, errorHandler)
    {
    }

    internal PicaClipboardShortcutDialog(IPicaDesktopStateService stateService,
        Func<CancellationToken, Task<IReadOnlyList<ViewerSettingContribution>>> createOptionsAsync,
        Func<CancellationToken, Task> enableAsync, IViewModelErrorHandler errorHandler)
    {
        _stateService = stateService ?? throw new ArgumentNullException(nameof(stateService));
        _createOptionsAsync = createOptionsAsync ?? throw new ArgumentNullException(nameof(createOptionsAsync));
        _enableAsync = enableAsync ?? throw new ArgumentNullException(nameof(enableAsync));
        _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
    }

    public async Task ShowIfNeededAsync(Window owner, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(owner);
        PicaDesktopState state = await _stateService.LoadAsync(ct);

        if (state.HasSeenClipboardShortcutPrompt || state.IsClipboardShortcutEnabled || !owner.IsVisible)
        {
            return;
        }

        Task operation = ShowCoreAsync(owner, ct);
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

    private async Task ShowCoreAsync(Window owner, CancellationToken ct)
    {
        IReadOnlyList<ViewerSettingContribution> options = await _createOptionsAsync(ct);
        ct.ThrowIfCancellationRequested();

        if (!owner.IsVisible)
        {
            return;
        }

        ClipboardShortcutOfferViewModel viewModel = new(_enableAsync, DismissAsync, _errorHandler);
        ClipboardShortcutOfferWindow window = new(viewModel, options);

        try
        {
            await DesktopDialogPresenter.ShowAsync(window, owner, ct);
        }
        finally
        {
            viewModel.EnableCommand.Cancel();
            viewModel.DeclineCommand.Cancel();

            if (viewModel.EnableCommand.ExecutionTask is { } enable)
            {
                await enable;
            }

            if (viewModel.DeclineCommand.ExecutionTask is { } decline)
            {
                await decline;
            }

            await window.SettingsCompletion;

            if (window.IsVisible)
            {
                window.Close();
            }
        }

        ct.ThrowIfCancellationRequested();
        await DismissAsync(ct);
    }

    private Task DismissAsync(CancellationToken ct)
    {
        return _stateService.UpdateAsync(state => state.HasSeenClipboardShortcutPrompt = true, ct);
    }
}
