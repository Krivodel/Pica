using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.Logging;
using SukiUI.Controls;
using Pica.Viewer.Services;
using Pica.Protocol;

namespace Pica.Viewer.Views;

public sealed partial class ImageViewerWindow : SukiWindow
{
    private void ShowOpenWithMenu(OpenWithTarget target, Control anchor)
    {
        if ((_currentItem is null) || !_platformFileActions.SupportsOpenWith)
        {
            return;
        }

        try
        {
            string associationFilePath = target == OpenWithTarget.Selection
                ? PicaImageFormats.SelectionFileName
                : _currentItem.FilePath;
            IReadOnlyList<OpenWithApplication> applications =
                _platformFileActions.GetOpenWithApplications(associationFilePath);
            _view.UpdateOpenWithApplications(
                applications,
                OnOpenWithApplicationClicked,
                OnChooseApplicationClicked);
            _logger.LogDebug(
                "Loaded {ApplicationCount} applications for Pica open-with target {Target}",
                applications.Count,
                target);
            _openWithTarget = target;
            _openWithAnchor = anchor;
            ShowOpenWithSubmenu(anchor);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve the application list for opening the image.");
        }
    }

    private async Task RunPlatformFileActionAsync(
        Func<CancellationToken, Task> action,
        string operationName)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        try
        {
            await action(CancellationToken.None);
            _logger.LogInformation(
                "Completed Pica system action {OperationName}",
                operationName);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to execute Pica system operation {OperationName}.",
                operationName);
        }
    }

    private void OnZoomOutClicked(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        Size viewport = GetViewportSize();
        BeginScaleAnimation(_scale / GetZoomButtonFactor(), new Point(viewport.Width / 2d, viewport.Height / 2d));
    }

    private void OnResetClicked(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        BeginResetScaleAndCenterAnimation();
    }

    private void OnZoomInClicked(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        Size viewport = GetViewportSize();
        BeginScaleAnimation(_scale * GetZoomButtonFactor(), new Point(viewport.Width / 2d, viewport.Height / 2d));
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        CloseWithFade();
    }

    private void OnWindowModeClicked(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        ToggleWindowMode();
    }

    private void OnSettingsClicked(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        if (_view.SettingsPanel is { IsVisible: true, IsHitTestVisible: true })
        {
            HideSettingsPanel();
            return;
        }

        ShowSettingsPanel();
    }

    private async void OnContextCopyClicked(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        HideContextMenu();
        await CopyCurrentImageAsync(CancellationToken.None);
    }

    private async void OnContextExternalActionClicked(object? sender, RoutedEventArgs e)
    {
        _ = e;

        PicaActionDefinition? action = GetExternalAction(sender);

        if (action is null)
        {
            return;
        }

        HideContextMenu();
        await DispatchCurrentImageActionAsync(action, CancellationToken.None);
    }

    private async void OnContextSaveAsClicked(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        HideContextMenu();
        await SaveCurrentImageAsAsync(CancellationToken.None);
    }

    private async void OnContextRevealInFolderClicked(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        if (_currentItem is null)
        {
            return;
        }

        string filePath = _currentItem.FilePath;
        FileRevealWindowMode windowMode =
            AlternateActionModifierPolicy.IsActive(_activeKeyModifiers)
                ? FileRevealWindowMode.OpenNew
                : FileRevealWindowMode.ReuseExisting;
        HideContextMenu();
        await RunPlatformFileActionAsync(
            ct => _platformFileActions.RevealInFolderAsync(
                filePath,
                windowMode,
                ct),
            "Reveal in folder");
    }

    private void OnContextOpenWithClicked(object? sender, RoutedEventArgs e)
    {
        _ = e;

        Control anchor = sender as Control ?? _view.ContextOpenWithButton;
        ShowOpenWithMenu(OpenWithTarget.CurrentImage, anchor);
    }

    private async void OnOpenWithApplicationClicked(object? sender, RoutedEventArgs e)
    {
        _ = e;

        if (sender is not Button { Tag: OpenWithApplication application })
        {
            return;
        }

        OpenWithTarget target = _openWithTarget;
        await RunExclusiveImageOperationAsync(
            ct => RunOpenWithTargetActionAsync(
                target,
                (filePath, actionCt) => _platformFileActions.OpenWithAsync(
                    filePath,
                    application,
                    actionCt),
                "Open with",
                ct),
            CancellationToken.None);
    }

    private async void OnChooseApplicationClicked(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        OpenWithTarget target = _openWithTarget;
        await RunExclusiveImageOperationAsync(
            ct => RunOpenWithTargetActionAsync(
                target,
                _platformFileActions.ChooseApplicationAsync,
                "Choose application",
                ct),
            CancellationToken.None);
    }

    private async Task RunOpenWithTargetActionAsync(
        OpenWithTarget target,
        Func<string, CancellationToken, Task> action,
        string actionName,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionName);

        string? filePath = await GetOpenWithFilePathAsync(target, ct);

        if (filePath is null)
        {
            return;
        }

        HideOpenWithAfterAction(target);
        await RunPlatformFileActionAsync(
            actionCt => action(filePath, actionCt),
            actionName);
    }

    private void OnContextSelectAreaClicked(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        HideContextMenu();
        HideViewerControls();
        _isSelectionArmed = true;
        UpdateSelectionCursor(_lastPointerHoverPosition);
    }

    private void OnSelectionCancelClicked(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        CancelSelection();
    }

    private void OnSelectionOpenWithClicked(object? sender, RoutedEventArgs e)
    {
        _ = e;

        Control anchor = sender as Control ?? _view.SelectionOpenWithButton;
        ShowOpenWithMenu(OpenWithTarget.Selection, anchor);
    }

    private async void OnSelectionCopyClicked(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        await CopySelectionAndCloseAsync(CancellationToken.None);
    }

    private async void OnSelectionExternalActionClicked(object? sender, RoutedEventArgs e)
    {
        _ = e;

        PicaActionDefinition? action = GetExternalAction(sender);

        if (action is null)
        {
            return;
        }

        await DispatchSelectionActionAndCloseAsync(action, CancellationToken.None);
    }

    private async void OnSelectionSaveAsClicked(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        await SaveSelectionAsAndCloseAsync(CancellationToken.None);
    }

    private static PicaActionDefinition? GetExternalAction(object? sender)
    {
        return sender is Button { Tag: PicaActionDefinition action } ? action : null;
    }
}
