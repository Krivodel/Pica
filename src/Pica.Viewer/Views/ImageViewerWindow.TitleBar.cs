using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.VisualTree;
using SukiUI.Controls;

using Pica.Viewer.Services;

namespace Pica.Viewer.Views;

public sealed partial class ImageViewerWindow : SukiWindow
{
    internal bool IsTitleBarOverlayEnabled => _autoHideWindowTitleBar;

    private const int TitleBarRevealDurationMilliseconds = 200;
    private const int TitleBarHideDurationMilliseconds = 120;
    private const uint EnterSizeMoveMessage = 0x0231;
    private const uint ExitSizeMoveMessage = 0x0232;

    private bool IsWindowGeometryInteractionActive => _isNativeWindowDragActive
        || (_interaction?.WindowResize.IsActive == true);

    private static readonly QuinticEaseIn TitleBarRevealEasing = new();

    private readonly ImageDoubleClickTracker _titleBarDoubleClickTracker =
        new();
    private readonly List<Visual> _titleBarRoleVisuals = [];
    private Control? _titleBarControl;
    private Panel? _titleBarLayoutParent;
    private Panel? _titleBarOverlayParent;
    private Button? _titleBarCloseButton;
    private Button? _titleBarMinimizeButton;
    private Button? _titleBarPinButton;
    private Point? _titleBarPointerPosition;
    private bool _autoHideWindowTitleBar;
    private bool _isNativeWindowDragActive;
    private long _titleBarOpacityAnimationId;

    internal void SetTitleBarAutoHide(bool autoHideWindowTitleBar)
    {
        _autoHideWindowTitleBar = autoHideWindowTitleBar;
        UpdateTitleBarLayout();
        UpdateTitleBarVisibility(animate: false);
    }

    internal void UpdateTitleBarVisibility()
    {
        UpdateTitleBarVisibility(animate: _autoHideWindowTitleBar && IsTitleBarVisible);
    }

    internal void SetNativeWindowDragActive(bool isActive)
    {
        _isNativeWindowDragActive = isActive;
        UpdateTitleBarVisibility();
    }

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
        _titleBarLayoutParent = _titleBarControl.Parent as Panel
            ?? throw new InvalidOperationException(
                "The Suki window title bar layout parent is missing.");
        _titleBarOverlayParent = e.NameScope.Find<Panel>("PART_Root")
            ?? throw new InvalidOperationException(
                "The Suki window root template part is missing.");
        _titleBarControl.Opacity = 0d;
        _titleBarControl.IsHitTestVisible = false;
        UpdateTitleBarLayout();
        UpdateTitleBarVisibility();
        AttachWindowResizeOverlay(e);
        DisableNativeTitleBarRoles(_titleBarControl);
        _titleBarControl.SizeChanged += OnTitleBarSizeChanged;
        AddHandler(
            PointerPressedEvent,
            OnTitleBarPointerPressed,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
        AddHandler(
            PointerMovedEvent,
            OnTitleBarPointerMoved,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
        PointerEntered += OnTitleBarPointerMoved;
        PointerExited += OnTitleBarPointerExited;
        Win32Properties.AddWndProcHookCallback(this, OnTitleBarWindowMessage);
    }

    private void UpdateTitleBarVisibility(bool animate)
    {
        if (_titleBarControl is null)
        {
            return;
        }

        bool isPointerOverTitleBar = _titleBarPointerPosition is Point pointerPosition
            && this.TranslatePoint(pointerPosition, _titleBarControl) is Point titleBarPosition
            && new Rect(_titleBarControl.Bounds.Size).Contains(titleBarPosition);
        bool isVisible = IsTitleBarVisible
            && (!_autoHideWindowTitleBar
                || (isPointerOverTitleBar && !IsWindowGeometryInteractionActive));
        Control titleBar = _titleBarControl;

        if (animate && (titleBar.IsHitTestVisible == isVisible))
        {
            return;
        }

        long animationId = ++_titleBarOpacityAnimationId;
        titleBar.IsHitTestVisible = isVisible;
        double targetOpacity = isVisible ? 1d : 0d;
        double startingOpacity = titleBar.Opacity;

        if (!animate || (startingOpacity == targetOpacity))
        {
            titleBar.Opacity = targetOpacity;
            return;
        }

        TimeSpan duration = TimeSpan.FromMilliseconds(isVisible
            ? TitleBarRevealDurationMilliseconds
            : TitleBarHideDurationMilliseconds);
        Interaction.AnimationRunner.Start(
            duration,
            () => animationId == _titleBarOpacityAnimationId,
            progress =>
            {
                double easedProgress = isVisible
                    ? TitleBarRevealEasing.Ease(progress)
                    : ViewerFrameAnimationRunner.EaseOutCubic(progress);
                titleBar.Opacity = startingOpacity
                    + ((targetOpacity - startingOpacity) * easedProgress);
            });
    }

    private void DetachTitleBarInteraction()
    {
        _titleBarOpacityAnimationId++;
        Win32Properties.RemoveWndProcHookCallback(this, OnTitleBarWindowMessage);

        if (_titleBarControl is not null)
        {
            RemoveHandler(
                PointerPressedEvent,
                OnTitleBarPointerPressed);
            RemoveHandler(PointerMovedEvent, OnTitleBarPointerMoved);
            PointerEntered -= OnTitleBarPointerMoved;
            PointerExited -= OnTitleBarPointerExited;
            _titleBarControl.SizeChanged -= OnTitleBarSizeChanged;

            if ((_titleBarControl.Parent == _titleBarOverlayParent)
                && (_titleBarLayoutParent is not null))
            {
                _titleBarOverlayParent?.Children.Remove(_titleBarControl);
                _titleBarLayoutParent.Children.Insert(0, _titleBarControl);
            }

            _titleBarControl = null;
        }

        _titleBarLayoutParent = null;
        _titleBarOverlayParent = null;
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

    private void UpdateTitleBarLayout()
    {
        if ((_titleBarControl is null)
            || (_titleBarLayoutParent is null)
            || (_titleBarOverlayParent is null))
        {
            return;
        }

        Panel targetParent = _autoHideWindowTitleBar
            ? _titleBarOverlayParent
            : _titleBarLayoutParent;

        if (_titleBarControl.Parent == targetParent)
        {
            return;
        }

        if (_titleBarControl.Parent is Panel currentParent)
        {
            currentParent.Children.Remove(_titleBarControl);
        }

        _titleBarControl.VerticalAlignment = VerticalAlignment.Top;

        if (_autoHideWindowTitleBar)
        {
            int resizeOverlayIndex = targetParent.Children.IndexOf(View.WindowResizeOverlay);
            targetParent.Children.Insert(
                resizeOverlayIndex >= 0 ? resizeOverlayIndex : targetParent.Children.Count,
                _titleBarControl);
        }
        else
        {
            targetParent.Children.Insert(0, _titleBarControl);
        }
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

    private void OnTitleBarPointerMoved(object? sender, PointerEventArgs e)
    {
        _ = sender;
        _titleBarPointerPosition = e.GetPosition(this);
        UpdateTitleBarVisibility();
    }

    private void OnTitleBarPointerExited(object? sender, PointerEventArgs e)
    {
        _ = sender;
        _ = e;
        _titleBarPointerPosition = null;
        UpdateTitleBarVisibility();
    }

    private void OnTitleBarSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        UpdateTitleBarVisibility();
    }

    private nint OnTitleBarWindowMessage(
        nint windowHandle,
        uint message,
        nint parameter,
        nint data,
        ref bool handled)
    {
        _ = windowHandle;
        _ = parameter;
        _ = data;
        _ = handled;

        if (message == EnterSizeMoveMessage)
        {
            SetNativeWindowDragActive(true);
        }
        else if (message == ExitSizeMoveMessage)
        {
            SetNativeWindowDragActive(false);
        }

        return 0;
    }
}
