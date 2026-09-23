using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using SukiUI.Controls;

using Pica.Viewer.Services;

namespace Pica.Viewer.Views;

public sealed partial class ImageViewerWindow : SukiWindow
{
    private readonly ImageDoubleClickTracker _titleBarDoubleClickTracker =
        new();
    private readonly List<Visual> _titleBarRoleVisuals = [];
    private Control? _titleBarControl;
    private Button? _titleBarCloseButton;
    private Button? _titleBarMinimizeButton;
    private Button? _titleBarPinButton;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        DetachTitleBarInteraction();
        base.OnApplyTemplate(e);

        _titleBarControl =
            e.NameScope.Find<Control>("PART_TitleBar")
            ?? throw new InvalidOperationException(
                "The Suki window title bar template part is missing.");
        _titleBarCloseButton = e.NameScope.Find<Button>("PART_CloseButton")
            ?? throw new InvalidOperationException(
                "The Suki window close button template part is missing.");
        _titleBarMinimizeButton =
            e.NameScope.Find<Button>("PART_MinimizeButton")
            ?? throw new InvalidOperationException(
                "The Suki window minimize button template part is missing.");
        _titleBarPinButton = e.NameScope.Find<Button>("PART_PinButton")
            ?? throw new InvalidOperationException(
                "The Suki window pin button template part is missing.");
        _titleBarCloseButton.IsEnabled = !_isSaving;
        _titleBarMinimizeButton.IsEnabled = !_isSaving;
        _titleBarPinButton.IsEnabled = !_isSaving;
        DisableNativeTitleBarRoles(_titleBarControl);
        AddHandler(
            PointerPressedEvent,
            OnTitleBarPointerPressed,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
    }

    private void DetachTitleBarInteraction()
    {
        if (_titleBarControl is not null)
        {
            RemoveHandler(
                PointerPressedEvent,
                OnTitleBarPointerPressed);
            _titleBarControl = null;
        }

        _titleBarCloseButton = null;
        _titleBarMinimizeButton = null;
        _titleBarPinButton = null;

        foreach (Visual visual in _titleBarRoleVisuals)
        {
            WindowDecorationProperties.SetElementRole(
                visual,
                WindowDecorationsElementRole.TitleBar);
        }

        _titleBarRoleVisuals.Clear();
        _titleBarDoubleClickTracker.Reset();
    }

    private void DisableNativeTitleBarRoles(Visual titleBar)
    {
        DisableNativeTitleBarRole(titleBar);

        foreach (Visual visual in titleBar.GetVisualDescendants())
        {
            DisableNativeTitleBarRole(visual);
        }
    }

    private void DisableNativeTitleBarRole(Visual visual)
    {
        if (WindowDecorationProperties.GetElementRole(visual)
            != WindowDecorationsElementRole.TitleBar)
        {
            return;
        }

        _titleBarRoleVisuals.Add(visual);
        WindowDecorationProperties.SetElementRole(
            visual,
            WindowDecorationsElementRole.None);
    }

    private void OnTitleBarPointerPressed(
        object? sender,
        PointerPressedEventArgs e)
    {
        _ = sender;

        if (!e.Properties.IsLeftButtonPressed
            || !IsFromTitleBarArea(e.Source))
        {
            return;
        }

        Point position = e.GetPosition(this);

        if (!_titleBarDoubleClickTracker.RegisterClick(
            position,
            DateTimeOffset.UtcNow))
        {
            return;
        }

        e.Handled = true;
        e.PreventGestureRecognition();
        _windowMode.Toggle();
    }

    private bool IsFromTitleBarArea(object? source)
    {
        for (
            Visual? visual = source as Visual;
            visual is not null;
            visual = visual.GetVisualParent())
        {
            if (WindowDecorationProperties.GetElementRole(visual)
                == WindowDecorationsElementRole.User)
            {
                return false;
            }

            if (visual == _titleBarControl)
            {
                return true;
            }
        }

        return false;
    }
}
