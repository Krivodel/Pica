using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

using Pica.Viewer.Services;
using Pica.Viewer.ViewModels;

namespace Pica.Viewer.Views;

internal sealed class ViewerFloatingMenuController : IDisposable
{
    internal OpenWithTarget OpenWithTarget => _openWithTarget;

    private const double MenuGap = 8d;
    private const double ContextMenuFallbackWidth = 172d;
    private const double ContextMenuFallbackHeight = 260d;
    private const double OpenWithMenuFallbackWidth = 220d;
    private const double OpenWithMenuFallbackHeight = 52d;
    private const double ToolMenuFallbackWidth = 160d;
    private const double ToolMenuFallbackHeight = 86d;
    private const double ModeMenuFallbackWidth = 160d;
    private const double ModeMenuFallbackHeight = 86d;
    private const int SubmenuHideDelayMilliseconds = 120;

    private readonly ImageViewerView _view;
    private readonly ImageViewerOpenWithViewModel _openWith;
    private readonly ImagePresentationController _imagePresentation;
    private readonly ImageViewportController _viewport;
    private readonly ImageSelectionController _selection;
    private readonly EventHandler<RoutedEventArgs>
        _openWithApplicationClicked;
    private readonly EventHandler<RoutedEventArgs>
        _chooseApplicationClicked;
    private readonly DispatcherTimer _submenuHideTimer;
    private OpenWithTarget _openWithTarget;
    private Control? _submenuAnchor;
    private Border? _activeSubmenu;

    internal ViewerFloatingMenuController(
        ImageViewerView view,
        ImageViewerOpenWithViewModel openWith,
        ImagePresentationController imagePresentation,
        ImageViewportController viewport,
        ImageSelectionController selection,
        EventHandler<RoutedEventArgs> openWithApplicationClicked,
        EventHandler<RoutedEventArgs> chooseApplicationClicked)
    {
        _view = view ?? throw new ArgumentNullException(nameof(view));
        _openWith = openWith
            ?? throw new ArgumentNullException(nameof(openWith));
        _imagePresentation = imagePresentation
            ?? throw new ArgumentNullException(nameof(imagePresentation));
        _viewport = viewport
            ?? throw new ArgumentNullException(nameof(viewport));
        _selection = selection
            ?? throw new ArgumentNullException(nameof(selection));
        _openWithApplicationClicked = openWithApplicationClicked
            ?? throw new ArgumentNullException(
                nameof(openWithApplicationClicked));
        _chooseApplicationClicked = chooseApplicationClicked
            ?? throw new ArgumentNullException(
                nameof(chooseApplicationClicked));
        _submenuHideTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(
                SubmenuHideDelayMilliseconds)
        };
        _submenuHideTimer.Tick += OnSubmenuHideTimerTick;
    }

    public void Dispose()
    {
        _submenuHideTimer.Stop();
        _submenuHideTimer.Tick -= OnSubmenuHideTimerTick;
    }

    internal void ShowContext(Point position)
    {
        if (_selection.IsActive || _selection.IsSelecting)
        {
            return;
        }

        HideTool();
        HideOpenWithSubmenu();
        _view.ViewerContextMenu.IsVisible = true;
        _view.ViewerContextMenu.Measure(
            new Size(double.PositiveInfinity, double.PositiveInfinity));
        Size menuSize = ViewerFloatingMenuPositioning.GetMeasuredSize(
            _view.ViewerContextMenu,
            new Size(
                ContextMenuFallbackWidth,
                ContextMenuFallbackHeight));
        Point menuPosition =
            ViewerFloatingMenuPositioning.CalculateContextPosition(
                position,
                menuSize,
                _viewport.GetViewportSize(),
                MenuGap);
        Canvas.SetLeft(_view.ViewerContextMenu, menuPosition.X);
        Canvas.SetTop(_view.ViewerContextMenu, menuPosition.Y);
        _view.ViewerContextMenu.Opacity =
            _view.VisibleControlsOpacity;
    }

    internal void HideContext()
    {
        _view.ViewerContextMenu.Opacity =
            _view.HiddenControlsOpacity;
        _view.ViewerContextMenu.IsVisible = false;
        HideOpenWithSubmenu();
    }

    internal void HideOpenWithAfterAction(OpenWithTarget target)
    {
        if (target == OpenWithTarget.CurrentImage)
        {
            HideContext();
            return;
        }

        HideOpenWithSubmenu();
    }

    internal async Task ShowOpenWithAsync(
        OpenWithTarget target,
        Control anchor)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        await LoadOpenWithApplicationsAsync(target);
        ShowLoadedOpenWithMenu(target, anchor);
    }

    internal void ToggleTool()
    {
        if (_view.ToolMenu.IsVisible)
        {
            HideTool();
            return;
        }

        ShowTool();
    }

    internal void HideTool()
    {
        HideModeSubmenu();
        _view.ToolMenu.Opacity = _view.HiddenControlsOpacity;
        _view.ToolMenu.IsVisible = false;
    }

    internal void HideAll()
    {
        HideContext();
        HideTool();
    }

    internal void ShowMode(Control anchor)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        CancelSubmenuHide();
        ShowModeSubmenu(anchor);
    }

    internal async void OnContextOpenWithAnchorPointerEntered(
        object? sender,
        PointerEventArgs e)
    {
        _ = e;

        if (sender is not Control anchor)
        {
            return;
        }

        CancelSubmenuHide();
        await LoadOpenWithApplicationsAsync(
            OpenWithTarget.CurrentImage);

        if (anchor.IsPointerOver)
        {
            ShowLoadedOpenWithMenu(
                OpenWithTarget.CurrentImage,
                anchor);
        }
    }

    internal void OnSubmenuAnchorPointerExited(
        object? sender,
        PointerEventArgs e)
    {
        _ = sender;
        _ = e;
        ScheduleSubmenuHide();
    }

    internal void OnSubmenuPointerEntered(
        object? sender,
        PointerEventArgs e)
    {
        _ = sender;
        _ = e;
        CancelSubmenuHide();
    }

    internal void OnSubmenuPointerExited(
        object? sender,
        PointerEventArgs e)
    {
        _ = sender;
        _ = e;
        ScheduleSubmenuHide();
    }

    internal void OnModeMenuAnchorPointerEntered(
        object? sender,
        PointerEventArgs e)
    {
        _ = e;

        if (!_view.ToolMenu.IsVisible
            || (sender is not Control anchor))
        {
            return;
        }

        CancelSubmenuHide();
        ShowModeSubmenu(anchor);
    }

    private async Task LoadOpenWithApplicationsAsync(
        OpenWithTarget target)
    {
        if ((_imagePresentation.CurrentItem is null)
            || !_openWith.IsSupported)
        {
            return;
        }

        await _openWith.LoadApplicationsCommand.ExecuteAsync(target);
    }

    private void ShowLoadedOpenWithMenu(
        OpenWithTarget target,
        Control anchor)
    {
        if ((_imagePresentation.CurrentItem is null)
            || !_openWith.IsSupported
            || !_openWith.HasLoadedApplications
            || (_openWith.LoadedTarget != target))
        {
            return;
        }

        _view.UpdateOpenWithApplications(
            _openWith.Applications,
            _openWithApplicationClicked,
            _chooseApplicationClicked);
        _openWithTarget = target;
        ShowOpenWithSubmenu(anchor);
    }

    private void ShowTool()
    {
        HideContext();
        HideModeSubmenu();
        _view.ToolMenu.IsVisible = true;
        _view.ToolMenu.Measure(
            new Size(double.PositiveInfinity, double.PositiveInfinity));
        Size menuSize = ViewerFloatingMenuPositioning.GetMeasuredSize(
            _view.ToolMenu,
            new Size(
                ToolMenuFallbackWidth,
                ToolMenuFallbackHeight));
        Point? translatedPosition =
            _view.ToolMenuButton.TranslatePoint(
                new Point(0d, 0d),
                _view.ToolMenuLayer);

        if (translatedPosition is not { } anchorPosition)
        {
            HideTool();
            return;
        }

        Point menuPosition =
            ViewerFloatingMenuPositioning.CalculateToolPosition(
                anchorPosition,
                _view.ToolMenuButton.Bounds.Size,
                menuSize,
                _viewport.GetViewportSize(),
                MenuGap);
        Canvas.SetLeft(_view.ToolMenu, menuPosition.X);
        Canvas.SetTop(_view.ToolMenu, menuPosition.Y);
        _view.ToolMenu.Opacity = _view.VisibleControlsOpacity;
    }

    private void ShowOpenWithSubmenu(Control anchor)
    {
        double menuGap = _openWithTarget
            == OpenWithTarget.Selection
            ? 0d
            : MenuGap;
        ShowSubmenu(
            _view.OpenWithMenu,
            _view.OpenWithMenuLayer,
            anchor,
            new Size(
                OpenWithMenuFallbackWidth,
                OpenWithMenuFallbackHeight),
            menuGap);
    }

    private void ShowModeSubmenu(Control anchor)
    {
        ShowSubmenu(
            _view.ModeMenu,
            _view.ToolMenuLayer,
            anchor,
            new Size(
                ModeMenuFallbackWidth,
                ModeMenuFallbackHeight),
            MenuGap);
    }

    private void ShowSubmenu(
        Border submenu,
        Canvas submenuLayer,
        Control anchor,
        Size fallbackSize,
        double menuGap)
    {
        ArgumentNullException.ThrowIfNull(submenu);
        ArgumentNullException.ThrowIfNull(submenuLayer);
        ArgumentNullException.ThrowIfNull(anchor);

        HideActiveSubmenu();
        _activeSubmenu = submenu;
        _submenuAnchor = anchor;

        Size viewport = _viewport.GetViewportSize();
        submenu.IsVisible = true;
        submenu.Measure(
            new Size(double.PositiveInfinity, double.PositiveInfinity));
        Size submenuSize =
            ViewerFloatingMenuPositioning.GetMeasuredSize(
                submenu,
                fallbackSize);
        Point? translatedPosition = anchor.TranslatePoint(
            new Point(0d, 0d),
            submenuLayer);

        if (translatedPosition is not { } anchorPosition)
        {
            HideActiveSubmenu();
            return;
        }

        double x =
            anchorPosition.X + anchor.Bounds.Width + menuGap;

        if ((x + submenuSize.Width) > viewport.Width)
        {
            x =
                anchorPosition.X - submenuSize.Width - menuGap;
        }

        double maxX =
            Math.Max(0d, viewport.Width - submenuSize.Width);
        double maxY =
            Math.Max(0d, viewport.Height - submenuSize.Height);
        Canvas.SetLeft(submenu, Math.Clamp(x, 0d, maxX));
        Canvas.SetTop(
            submenu,
            Math.Clamp(anchorPosition.Y, 0d, maxY));
        submenu.Opacity = _view.VisibleControlsOpacity;
    }

    private void HideOpenWithSubmenu()
    {
        HideSubmenu(_view.OpenWithMenu);
    }

    private void HideModeSubmenu()
    {
        HideSubmenu(_view.ModeMenu);
    }

    private void HideActiveSubmenu()
    {
        Border? submenu = _activeSubmenu;

        if (submenu is not null)
        {
            HideSubmenu(submenu);
        }
    }

    private void HideSubmenu(Border submenu)
    {
        ArgumentNullException.ThrowIfNull(submenu);

        submenu.Opacity = _view.HiddenControlsOpacity;
        submenu.IsVisible = false;

        if (object.ReferenceEquals(_activeSubmenu, submenu))
        {
            _submenuHideTimer.Stop();
            _activeSubmenu = null;
            _submenuAnchor = null;
        }
    }

    private void ScheduleSubmenuHide()
    {
        _submenuHideTimer.Stop();
        _submenuHideTimer.Start();
    }

    private void CancelSubmenuHide()
    {
        _submenuHideTimer.Stop();
    }

    private void OnSubmenuHideTimerTick(
        object? sender,
        EventArgs e)
    {
        _ = sender;
        _ = e;
        _submenuHideTimer.Stop();

        bool isAnchorHovered =
            _submenuAnchor?.IsPointerOver == true;
        bool isSubmenuHovered =
            _activeSubmenu?.IsPointerOver == true;

        if (!isAnchorHovered && !isSubmenuHovered)
        {
            HideActiveSubmenu();
        }
    }
}
