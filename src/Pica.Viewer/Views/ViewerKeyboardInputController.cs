using Avalonia;
using Avalonia.Input;

using Pica.Viewer.Services;
using Pica.Viewer.Controls;
using Pica.Viewer.ViewModels;

namespace Pica.Viewer.Views;

internal sealed class ViewerKeyboardInputController
{
    internal KeyModifiers ActiveKeyModifiers =>
        _activeKeyModifiers;
    internal double ZoomButtonFactor =>
        Math.Pow(
            ZoomButtonStepBase,
            GetEffectiveZoomSpeed(_activeKeyModifiers));

    private const double DefaultZoomButtonFactor = 1.2d;

    private static readonly double ZoomButtonStepBase =
        Math.Pow(
            DefaultZoomButtonFactor,
            1d / ViewerSettingsDefaults.ZoomSpeed);

    private readonly ImageViewerView _view;
    private readonly ImageViewerSessionViewModel _session;
    private readonly ImageViewerSettingsViewModel _settings;
    private readonly ImageViewportController _viewport;
    private readonly ImageSelectionController _selection;
    private readonly ViewerSelectionInteractionController
        _selectionInteraction;
    private readonly ImageViewerActionController _actions;
    private readonly ViewerChromeVisibilityController _chromeVisibility;
    private readonly ViewerSettingsPanelController _settingsPanel;
    private readonly ViewerPointerInputController _pointerInput;
    private readonly Action _close;
    private IViewerClipboardShortcut? _clipboardShortcut;
    private KeyModifiers _activeKeyModifiers;

    internal ViewerKeyboardInputController(
        ImageViewerView view,
        ImageViewerSessionViewModel session,
        ImageViewerSettingsViewModel settings,
        ImageViewportController viewport,
        ImageSelectionController selection,
        ViewerSelectionInteractionController selectionInteraction,
        ImageViewerActionController actions,
        ViewerChromeVisibilityController chromeVisibility,
        ViewerSettingsPanelController settingsPanel,
        ViewerPointerInputController pointerInput,
        Action close)
    {
        _view = view ?? throw new ArgumentNullException(nameof(view));
        _session = session
            ?? throw new ArgumentNullException(nameof(session));
        _settings = settings
            ?? throw new ArgumentNullException(nameof(settings));
        _viewport = viewport
            ?? throw new ArgumentNullException(nameof(viewport));
        _selection = selection
            ?? throw new ArgumentNullException(nameof(selection));
        _selectionInteraction = selectionInteraction
            ?? throw new ArgumentNullException(
                nameof(selectionInteraction));
        _actions = actions
            ?? throw new ArgumentNullException(nameof(actions));
        _chromeVisibility = chromeVisibility
            ?? throw new ArgumentNullException(nameof(chromeVisibility));
        _settingsPanel = settingsPanel
            ?? throw new ArgumentNullException(nameof(settingsPanel));
        _pointerInput = pointerInput
            ?? throw new ArgumentNullException(nameof(pointerInput));
        _close = close ?? throw new ArgumentNullException(nameof(close));
    }

    internal void SetClipboardShortcut(IViewerClipboardShortcut? shortcut)
    {
        _clipboardShortcut = shortcut;
    }

    internal void ResetModifiersAfterRecording()
    {
        _activeKeyModifiers = KeyModifiers.None;
        _chromeVisibility.SetControlModifierActive(false);
        _chromeVisibility.Update(_pointerInput.LastPointerPosition);
    }

    internal async void OnPreviewKeyDown(
        object? sender,
        KeyEventArgs e)
    {
        _ = sender;

        if (ViewerSettingRecording.IsActive(Avalonia.Controls.TopLevel.GetTopLevel(_view.SettingsPanel)))
        {
            e.Handled = true;
            return;
        }

        ViewerKeyboardAction action = ViewerKeyboardShortcutPolicy.Resolve(e.Key, e.PhysicalKey, e.KeyModifiers);

        if ((action == ViewerKeyboardAction.Paste)
            || (_clipboardShortcut?.Matches(e.Key, e.PhysicalKey, e.KeyModifiers) == true))
        {
            e.Handled = true;

            await _actions.PasteFromClipboardAsync(CancellationToken.None);

            return;
        }

        if (action != ViewerKeyboardAction.ToggleImageMode)
        {
            return;
        }

        if (!_actions.IsRunning)
        {
            _session.ToggleImageModeCommand.Execute(null);
        }

        e.Handled = true;
    }

    internal async void OnKeyDown(
        object? sender,
        KeyEventArgs e)
    {
        _ = sender;

        if (_actions.IsRunning || ViewerSettingRecording.IsActive(Avalonia.Controls.TopLevel.GetTopLevel(_view.SettingsPanel)))
        {
            e.Handled = true;
            return;
        }

        ViewerKeyboardAction action = ViewerKeyboardShortcutPolicy.Resolve(e.Key, e.PhysicalKey, e.KeyModifiers);

        _activeKeyModifiers = e.KeyModifiers;
        bool isControlModifierActive =
            ViewerInputModifiers.IsControlPressed(
                e.KeyModifiers)
            || ViewerInputModifiers.IsControlKey(e.Key);
        _chromeVisibility.SetControlModifierActive(
            isControlModifierActive);

        if (isControlModifierActive)
        {
            _chromeVisibility.HideControls();
        }

        if (action == ViewerKeyboardAction.Escape)
        {
            HandleEscape(e);
            return;
        }

        if (_session.IsClipboardImageActive && ViewerKeyboardShortcutPolicy.TryGetNavigationDirection(e.Key, out int returnDirection))
        {
            Navigate(returnDirection, e.KeyModifiers);
            e.Handled = true;
            return;
        }

        if ((_selection.IsActive || _selection.IsArmed)
            && (e.Key == Key.A)
            && ViewerInputModifiers.IsControlPressed(
                e.KeyModifiers))
        {
            _selectionInteraction.SelectEntireImage();
            e.Handled = true;
            return;
        }

        if (action == ViewerKeyboardAction.ToggleFiltering)
        {
            _settings.ToggleFilteringCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (action == ViewerKeyboardAction.ToggleBackground)
        {
            _settings.ToggleCheckerboardBackgroundCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (ViewerKeyboardShortcutPolicy.TryGetFrameNavigationDirection(
            e.Key,
            e.PhysicalKey,
            out int frameNavigationDirection))
        {
            _session.NavigateFrameCommand.Execute(
                frameNavigationDirection);
            e.Handled = true;
            return;
        }

        if (_selection.IsActive)
        {
            await HandleSelectionKeyDownAsync(e);
            return;
        }

        if (action == ViewerKeyboardAction.ResetViewport)
        {
            _viewport.BeginResetScaleAndCenterAnimation();
            e.Handled = true;
        }
        else if (ViewerKeyboardShortcutPolicy.TryGetNavigationDirection(
            e.Key,
            out int navigationDirection))
        {
            Navigate(
                navigationDirection,
                e.KeyModifiers);
            e.Handled = true;
        }
        else if (action == ViewerKeyboardAction.Copy)
        {
            await _actions.CopyCurrentWithFeedbackAsync(
                CancellationToken.None);
            e.Handled = true;
        }
    }

    internal void OnKeyUp(
        object? sender,
        KeyEventArgs e)
    {
        _ = sender;

        if (ViewerSettingRecording.IsActive(Avalonia.Controls.TopLevel.GetTopLevel(_view.SettingsPanel)))
        {
            e.Handled = true;
            return;
        }

        _activeKeyModifiers = e.KeyModifiers;
        bool isControlModifierActive =
            ViewerInputModifiers.IsControlPressed(
                e.KeyModifiers);
        _chromeVisibility.SetControlModifierActive(
            isControlModifierActive);

        if (isControlModifierActive)
        {
            _chromeVisibility.HideControls();
            return;
        }

        _chromeVisibility.Update(
            _pointerInput.LastPointerPosition);
    }

    private int GetEffectiveZoomSpeed(
        KeyModifiers modifiers)
    {
        return ViewerInputModifiers
            .IsBaseZoomSpeedRequested(modifiers)
            ? ViewerSettingsDefaults.MinimumSpeed
            : _settings.ZoomSpeed;
    }

    private void Navigate(
        int direction,
        KeyModifiers modifiers)
    {
        if (AlternateActionModifierPolicy.IsActive(modifiers))
        {
            _session.NavigateContentCommand.Execute(direction);
            return;
        }

        _session.NavigateCommand.Execute(direction);
    }

    private void HandleEscape(KeyEventArgs e)
    {
        ImageViewerInputState inputState = new(
            _view.SettingsPanel.IsVisible,
            _selection.IsActive || _selection.IsArmed,
            _session.IsChannelModeActive);
        ViewerEscapeAction escapeAction =
            ImageViewerInputPolicy.ResolveEscapeAction(inputState);

        switch (escapeAction)
        {
            case ViewerEscapeAction.HideSettings:
                _settingsPanel.Hide();
                break;
            case ViewerEscapeAction.CancelAreaSelection:
                _selectionInteraction.Cancel();
                break;
            case ViewerEscapeAction.ExitChannelMode:
                _session.SelectMainImageModeCommand.Execute(null);
                break;
            case ViewerEscapeAction.CloseViewer:
                _close();
                break;
            default:
                throw new InvalidOperationException(
                    $"Unsupported viewer escape action '{escapeAction}'.");
        }

        e.Handled = true;
    }

    private async Task HandleSelectionKeyDownAsync(
        KeyEventArgs e)
    {
        if ((e.Key == Key.C)
            && ViewerInputModifiers.IsControlPressed(
                e.KeyModifiers))
        {
            await _actions.CopySelectionAndCloseAsync(
                CancellationToken.None);
            e.Handled = true;
        }
        else if ((_session.IsChannelModeActive
                || AlternateActionModifierPolicy.IsActive(
                    e.KeyModifiers))
            && ViewerKeyboardShortcutPolicy.TryGetNavigationDirection(
                e.Key,
                out int channelDirection))
        {
            Navigate(
                channelDirection,
                e.KeyModifiers);
            e.Handled = true;
        }
    }
}
