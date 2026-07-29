using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

using Pica.Viewer.Resources;

namespace Pica.Viewer.Views;

internal sealed class ViewerCursorController : IDisposable
{
    internal bool IsHidden => _isHidden;

    private const int HideDelayMilliseconds = 1000;

    private readonly TopLevel _topLevel;
    private readonly ImageViewerView _view;
    private readonly ImageSelectionController _selection;
    private readonly ImageViewportController _viewport;
    private readonly DispatcherTimer _timer;
    private Point _lastPointerPosition;
    private bool _isPointerPressed;
    private bool _isHidden;

    internal ViewerCursorController(
        TopLevel topLevel,
        ImageViewerView view,
        ImageSelectionController selection,
        ImageViewportController viewport)
    {
        _topLevel = topLevel
            ?? throw new ArgumentNullException(nameof(topLevel));
        _view = view ?? throw new ArgumentNullException(nameof(view));
        _selection = selection
            ?? throw new ArgumentNullException(nameof(selection));
        _viewport = viewport
            ?? throw new ArgumentNullException(nameof(viewport));
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(HideDelayMilliseconds)
        };
        _timer.Tick += OnTimerTick;
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= OnTimerTick;
    }

    internal void Start()
    {
        _timer.Start();
    }

    internal void Stop()
    {
        _timer.Stop();
    }

    internal void Show()
    {
        _timer.Stop();

        if (IsAreaSelectionModeActive())
        {
            return;
        }

        SetVisible(ViewerCursors.Arrow);

        if (_view.ViewerArea.IsPointerOver
            && !_view.SettingsPanel.IsPointerOver)
        {
            _timer.Start();
        }
    }

    internal void ResetAfterPointerExit()
    {
        _timer.Stop();
        SetVisible(ViewerCursors.Arrow);
    }

    internal void UpdateSelection(
        Point position,
        bool isPointerPressed)
    {
        _lastPointerPosition = position;
        _isPointerPressed = isPointerPressed;
        _selection.UpdateCursor(
            position,
            isPointerPressed,
            _viewport.IsPanning,
            _isHidden,
            SetVisible);
    }

    internal void SetVisible(Cursor cursor)
    {
        ArgumentNullException.ThrowIfNull(cursor);
        _isHidden = false;
        _topLevel.Cursor = cursor;
    }

    private bool IsAreaSelectionModeActive()
    {
        return _selection.IsActive
            || _selection.IsSelecting
            || _selection.IsArmed;
    }

    private void Hide()
    {
        _isHidden = true;
        _topLevel.Cursor = ViewerCursors.Hidden;
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        if (IsAreaSelectionModeActive())
        {
            _timer.Stop();
            UpdateSelection(
                _lastPointerPosition,
                _isPointerPressed);
            return;
        }

        if (!_view.Root.IsPointerOver
            || _view.SettingsPanel.IsPointerOver)
        {
            SetVisible(ViewerCursors.Arrow);
            _timer.Stop();
            return;
        }

        Hide();
    }
}
