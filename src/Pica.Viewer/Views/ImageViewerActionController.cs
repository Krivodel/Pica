using System.ComponentModel;

using Avalonia.Controls;

using Pica.Protocol;
using Pica.Viewer.Services;
using Pica.Viewer.ViewModels;

namespace Pica.Viewer.Views;

internal sealed class ImageViewerActionController : IDisposable
{
    internal bool IsRunning => _actionGate.IsRunning;
    internal bool IsSaving => _owner.IsSaving;

    private const double CopyFeedbackOpacity = 0.44d;
    private const double CopyFeedbackFadeInDurationSeconds = 0.1d;
    private const double CopyFeedbackFadeOutDurationSeconds = 0.08d;

    private static readonly TimeSpan CopyFeedbackFadeInDuration =
        TimeSpan.FromSeconds(CopyFeedbackFadeInDurationSeconds);
    private static readonly TimeSpan CopyFeedbackFadeOutDuration =
        TimeSpan.FromSeconds(CopyFeedbackFadeOutDurationSeconds);
    private static readonly TimeSpan SaveStatusFadeDuration =
        TimeSpan.FromMilliseconds(220d);

    private readonly ViewerActionGate _actionGate;
    private readonly ImageViewerWindow _owner;
    private readonly ImageViewerView _view;
    private readonly ImageViewerActionsViewModel _actions;
    private readonly ImageViewerOpenWithViewModel _openWith;
    private readonly ImagePresentationController _imagePresentation;
    private readonly IImagePresentationReadiness _presentationReadiness;
    private readonly ImageSelectionController _selection;
    private readonly ViewerFrameAnimationRunner _animationRunner;
    private readonly Action _cancelSelection;
    private readonly Action<OpenWithTarget> _hideOpenWithAfterAction;
    private long _copyFeedbackAnimationId;
    private long _saveStatusAnimationId;
    private bool _isDisposed;

    internal ImageViewerActionController(
        ImageViewerWindow owner,
        ImageViewerView view,
        ImageViewerActionsViewModel actions,
        ImageViewerOpenWithViewModel openWith,
        ImagePresentationController imagePresentation,
        IImagePresentationReadiness presentationReadiness,
        ImageSelectionController selection,
        ViewerFrameAnimationRunner animationRunner,
        Action cancelSelection,
        Action<OpenWithTarget> hideOpenWithAfterAction)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _actionGate = new ViewerActionGate(owner);
        _view = view ?? throw new ArgumentNullException(nameof(view));
        _actions = actions ?? throw new ArgumentNullException(nameof(actions));
        _openWith = openWith
            ?? throw new ArgumentNullException(nameof(openWith));
        _imagePresentation = imagePresentation
            ?? throw new ArgumentNullException(nameof(imagePresentation));
        _presentationReadiness = presentationReadiness
            ?? throw new ArgumentNullException(nameof(presentationReadiness));
        _selection = selection
            ?? throw new ArgumentNullException(nameof(selection));
        _animationRunner = animationRunner
            ?? throw new ArgumentNullException(nameof(animationRunner));
        _cancelSelection = cancelSelection
            ?? throw new ArgumentNullException(nameof(cancelSelection));
        _hideOpenWithAfterAction = hideOpenWithAfterAction
            ?? throw new ArgumentNullException(nameof(hideOpenWithAfterAction));
        _actions.PropertyChanged += OnActionsPropertyChanged;
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _actions.PropertyChanged -= OnActionsPropertyChanged;
        _copyFeedbackAnimationId++;
        _saveStatusAnimationId++;
    }

    internal async Task CopyCurrentAsync(CancellationToken ct)
    {
        await _actionGate.RunAsync(CopyCurrentCoreAsync, ct);
    }

    internal async Task CopyCurrentWithFeedbackAsync(CancellationToken ct)
    {
        await CopyCurrentAsync(ct);
        await ShowCopyFeedbackAsync();
    }

    internal async Task DispatchCurrentAsync(
        PicaActionDefinition action,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(action);
        ct.ThrowIfCancellationRequested();
        await _actions.DispatchCurrentCommand.ExecuteAsync(action);
    }

    internal async Task SaveCurrentAsAsync(CancellationToken ct)
    {
        await RunSaveAsync(SaveCurrentCoreAsync, ct);
    }

    internal async Task RevealInFolderAsync(
        FileRevealWindowMode windowMode,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (_imagePresentation.CurrentItem is null)
        {
            return;
        }

        await _actions.RevealInFolderCommand.ExecuteAsync(windowMode);
    }

    internal async Task CopySelectionAndCloseAsync(CancellationToken ct)
    {
        await _actionGate.RunAsync(
            async operationCt =>
            {
                await CopySelectionAsync(operationCt);
                _cancelSelection();
            },
            ct);
    }

    internal async Task DispatchSelectionAndCloseAsync(
        PicaActionDefinition action,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (!await WaitForCurrentImagePresentationAsync(ct))
        {
            return;
        }

        PicaImageItem? item = _imagePresentation.CurrentItem;
        PreparedClipboardImage? image =
            await _selection.GetPreparedClipboardImageAsync(ct);

        if ((item is null) || (image is null))
        {
            return;
        }

        PreparedSelectionAction selectionAction = new(
            action,
            item,
            image);
        _cancelSelection();
        await _actions.DispatchSelectionCommand.ExecuteAsync(
            selectionAction);
    }

    internal async Task SaveSelectionAsAndCloseAsync(CancellationToken ct)
    {
        await RunSaveAsync(SaveSelectionAsCoreAsync, ct);
    }

    internal async Task OpenWithApplicationAsync(
        OpenWithTarget target,
        OpenWithApplication application,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(application);

        await _actionGate.RunAsync(
            async operationCt =>
            {
                await PrepareOpenWithFileAsync(target, operationCt);

                if (!_openWith.IsPrepared)
                {
                    return;
                }

                _hideOpenWithAfterAction(target);
                await _openWith.OpenWithApplicationCommand.ExecuteAsync(
                    application);
            },
            ct);
    }

    internal async Task ChooseApplicationAsync(
        OpenWithTarget target,
        CancellationToken ct)
    {
        await _actionGate.RunAsync(
            async operationCt =>
            {
                await PrepareOpenWithFileAsync(target, operationCt);

                if (!_openWith.IsPrepared)
                {
                    return;
                }

                _hideOpenWithAfterAction(target);
                await _openWith.ChooseApplicationCommand.ExecuteAsync(null);
            },
            ct);
    }

    private async Task RunSaveAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken ct)
    {
        await _actionGate.RunAsync(
            async operationCt =>
            {
                try
                {
                    _owner.SetSavingInteractionState(true);
                    await operation(operationCt);
                }
                finally
                {
                    _owner.SetSavingInteractionState(false);
                }
            },
            ct,
            blockWindowInteraction: false);
    }

    private async Task CopyCurrentCoreAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await _actions.CopyCurrentCommand.ExecuteAsync(null);
    }

    private async Task CopySelectionAsync(CancellationToken ct)
    {
        if (!await WaitForCurrentImagePresentationAsync(ct))
        {
            return;
        }

        PreparedClipboardImage? image =
            await _selection.GetPreparedClipboardImageAsync(ct);

        if (image is null)
        {
            return;
        }

        await _actions.CopySelectionCommand.ExecuteAsync(image);
    }

    private async Task SaveCurrentCoreAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await _actions.SaveCurrentCommand.ExecuteAsync(null);
    }

    private async Task SaveSelectionAsCoreAsync(CancellationToken ct)
    {
        if (!await WaitForCurrentImagePresentationAsync(ct))
        {
            return;
        }

        PreparedClipboardImage? image =
            await _selection.GetPreparedClipboardImageAsync(ct);

        if (image is null)
        {
            return;
        }

        await _actions.SaveSelectionCommand.ExecuteAsync(image);

        if (_actions.WasSelectionSaved)
        {
            _cancelSelection();
        }
    }

    private async Task PrepareOpenWithFileAsync(
        OpenWithTarget target,
        CancellationToken ct)
    {
        if (target == OpenWithTarget.CurrentImage)
        {
            ct.ThrowIfCancellationRequested();
            await _openWith.PrepareCurrentImageCommand.ExecuteAsync(null);
            return;
        }

        if (!await WaitForCurrentImagePresentationAsync(ct))
        {
            return;
        }

        PreparedClipboardImage? image =
            await _selection.GetPreparedClipboardImageAsync(ct);

        if (image is null)
        {
            return;
        }

        await _openWith.PrepareSelectionCommand.ExecuteAsync(image);
    }

    private async Task<bool> WaitForCurrentImagePresentationAsync(
        CancellationToken ct)
    {
        await _presentationReadiness.WaitAsync(ct);

        return _presentationReadiness.IsReady;
    }

    private async Task ShowCopyFeedbackAsync()
    {
        long animationId = ++_copyFeedbackAnimationId;

        await AnimateCopyFeedbackOpacityAsync(
            animationId,
            _view.HiddenControlsOpacity,
            CopyFeedbackOpacity,
            CopyFeedbackFadeInDuration);
        await AnimateCopyFeedbackOpacityAsync(
            animationId,
            CopyFeedbackOpacity,
            _view.HiddenControlsOpacity,
            CopyFeedbackFadeOutDuration);
    }

    private void OnActionsPropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ImageViewerActionsViewModel.IsSaveTakingLong))
        {
            AnimateSaveStatus();
        }
    }

    private void AnimateSaveStatus()
    {
        Border status = _view.SaveStatus;
        bool show = _actions.IsSaveTakingLong;
        double startingOpacity = status.Opacity;
        double startingOffset = _view.SaveStatusPanelTransform.Y;
        double targetOpacity = show
            ? _view.VisibleControlsOpacity
            : _view.HiddenControlsOpacity;
        double targetOffset = show
            ? 0d
            : ImageViewerVisualMetrics.SaveStatusHiddenOffset;
        long animationId = ++_saveStatusAnimationId;

        if (show)
        {
            status.IsVisible = true;
        }

        _animationRunner.Start(
            SaveStatusFadeDuration,
            () => !_isDisposed && animationId == _saveStatusAnimationId,
            progress =>
            {
                double easedProgress =
                    ViewerFrameAnimationRunner.EaseOutCubic(progress);
                status.Opacity = startingOpacity
                    + ((targetOpacity - startingOpacity) * easedProgress);
                _view.SaveStatusPanelTransform.Y = startingOffset
                    + ((targetOffset - startingOffset) * easedProgress);
            },
            completed: () =>
            {
                if (!show)
                {
                    status.IsVisible = false;
                }
            });
    }

    private Task AnimateCopyFeedbackOpacityAsync(
        long animationId,
        double from,
        double to,
        TimeSpan duration)
    {
        TaskCompletionSource completion = new();
        _animationRunner.Start(
            duration,
            () => animationId == _copyFeedbackAnimationId,
            progress =>
            {
                _view.FadeOverlay.Opacity =
                    from + ((to - from) * progress);
            },
            () => completion.TrySetResult(),
            () => completion.TrySetResult());

        return completion.Task;
    }
}
