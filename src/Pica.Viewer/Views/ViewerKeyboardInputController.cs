using Avalonia;
using Avalonia.Input;

using Pica.Viewer.Services;
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

    internal void OnPreviewKeyDown(
        object? sender,
        KeyEventArgs e)
    {
        _ = sender;

        if (e.Key != Key.Tab)
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

        if (_actions.IsRunning)
        {
            e.Handled = true;
            return;
        }

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

        if (e.Key == Key.Escape)
        {
            HandleEscape(e);
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

        if (e.Key == Key.F)
        {
            _settings.ToggleFilteringCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (_selection.IsActive)
        {
            await HandleSelectionKeyDownAsync(e);
            return;
        }

        if (e.Key == Key.Space)
        {
            _viewport.BeginResetScaleAndCenterAnimation();
            e.Handled = true;
        }
        else if (TryGetNavigationDirection(
            e.Key,
            out int navigationDirection))
        {
            _session.NavigateCommand.Execute(
                navigationDirection);
            e.Handled = true;
        }
        else if ((e.Key == Key.C)
            && ViewerInputModifiers.IsControlPressed(
                e.KeyModifiers))
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

    private static bool TryGetNavigationDirection(
        Key key,
        out int direction)
    {
        if ((key == Key.Left) || (key == Key.A))
        {
            direction = -1;
            return true;
        }

        if ((key == Key.Right) || (key == Key.D))
        {
            direction = 1;
            return true;
        }

        direction = 0;
        return false;
    }

    private int GetEffectiveZoomSpeed(
        KeyModifiers modifiers)
    {
        return ViewerInputModifiers
            .IsBaseZoomSpeedRequested(modifiers)
            ? ViewerSettingsDefaults.MinimumSpeed
            : _settings.ZoomSpeed;
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
        else if (_session.IsChannelModeActive
            && TryGetNavigationDirection(
                e.Key,
                out int channelDirection))
        {
            _session.NavigateCommand.Execute(channelDirection);
            e.Handled = true;
        }
    }
}
