using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SukiUI.Controls;

using Pica.Protocol;
using Pica.Viewer.Services;

namespace Pica.Viewer.Views;

public sealed partial class ImageViewerWindow : SukiWindow
{
    private static PicaActionDefinition? GetExternalAction(object? sender)
    {
        return sender is Button { Tag: PicaActionDefinition action } ? action : null;
    }

    private void OnToolMenuClicked(
        object? sender,
        RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        _floatingMenus.ToggleTool();
    }

    private void OnToolMenuActionClicked(
        object? sender,
        RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        _floatingMenus.HideTool();
    }

    private void OnModeMenuClicked(
        object? sender,
        RoutedEventArgs e)
    {
        _ = e;

        Control anchor =
            sender as Control ?? View.ModeMenuButton;
        _floatingMenus.ShowMode(anchor);
    }

    private void OnZoomOutClicked(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        Size viewport = _viewport.GetViewportSize();
        _viewport.BeginScaleAnimation(
            _viewport.Scale / _keyboardInput.ZoomButtonFactor,
            new Point(viewport.Width / 2d, viewport.Height / 2d));
    }

    private void OnResetClicked(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        _viewport.BeginResetScaleAndCenterAnimation();
    }

    private void OnZoomInClicked(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        Size viewport = _viewport.GetViewportSize();
        _viewport.BeginScaleAnimation(
            _viewport.Scale * _keyboardInput.ZoomButtonFactor,
            new Point(viewport.Width / 2d, viewport.Height / 2d));
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        Close();
    }

    private void OnWindowModeClicked(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        _windowMode.Toggle();
    }

    private void OnSettingsClicked(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        _settingsPanel.Toggle();
    }

    private async void OnContextCopyClicked(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        _floatingMenus.HideContext();
        await _actionController.CopyCurrentAsync(CancellationToken.None);
    }

    private async void OnContextExternalActionClicked(object? sender, RoutedEventArgs e)
    {
        _ = e;

        PicaActionDefinition? action = GetExternalAction(sender);

        if (action is null)
        {
            return;
        }

        _floatingMenus.HideContext();
        await _actionController.DispatchCurrentAsync(
            action,
            CancellationToken.None);
    }

    private async void OnContextSaveAsClicked(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        _floatingMenus.HideContext();
        await _actionController.SaveCurrentAsAsync(CancellationToken.None);
    }

    private async void OnContextRevealInFolderClicked(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        FileRevealWindowMode windowMode =
            AlternateActionModifierPolicy.IsActive(
                _keyboardInput.ActiveKeyModifiers)
                ? FileRevealWindowMode.OpenNew
                : FileRevealWindowMode.ReuseExisting;
        _floatingMenus.HideContext();
        await _actionController.RevealInFolderAsync(
            windowMode,
            CancellationToken.None);
    }

    private async void OnContextOpenWithClicked(
        object? sender,
        RoutedEventArgs e)
    {
        _ = e;

        Control anchor = sender as Control ?? View.ContextOpenWithButton;
        await _floatingMenus.ShowOpenWithAsync(
            OpenWithTarget.CurrentImage,
            anchor);
    }

    private async void OnOpenWithApplicationClicked(object? sender, RoutedEventArgs e)
    {
        _ = e;

        if (sender is not Button { Tag: OpenWithApplication application })
        {
            return;
        }

        OpenWithTarget target = _floatingMenus.OpenWithTarget;
        await _actionController.OpenWithApplicationAsync(
            target,
            application,
            CancellationToken.None);
    }

    private async void OnChooseApplicationClicked(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        OpenWithTarget target = _floatingMenus.OpenWithTarget;
        await _actionController.ChooseApplicationAsync(
            target,
            CancellationToken.None);
    }

    private void OnContextSelectAreaClicked(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        _floatingMenus.HideContext();
        _selectionInteraction.Arm();
    }

    private void OnSelectionCancelClicked(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        _selectionInteraction.Cancel();
    }

    private async void OnSelectionOpenWithClicked(
        object? sender,
        RoutedEventArgs e)
    {
        _ = e;

        Control anchor = sender as Control ?? View.SelectionOpenWithButton;
        await _floatingMenus.ShowOpenWithAsync(
            OpenWithTarget.Selection,
            anchor);
    }

    private async void OnSelectionCopyClicked(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        await _actionController.CopySelectionAndCloseAsync(
            CancellationToken.None);
    }

    private async void OnSelectionExternalActionClicked(object? sender, RoutedEventArgs e)
    {
        _ = e;

        PicaActionDefinition? action = GetExternalAction(sender);

        if (action is null)
        {
            return;
        }

        await _actionController.DispatchSelectionAndCloseAsync(
            action,
            CancellationToken.None);
    }

    private async void OnSelectionSaveAsClicked(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        await _actionController.SaveSelectionAsAndCloseAsync(
            CancellationToken.None);
    }

    private void OnFloatingMenuPointerPressed(
        object? sender,
        PointerPressedEventArgs e)
    {
        _ = sender;
        e.Handled = true;
    }
}
