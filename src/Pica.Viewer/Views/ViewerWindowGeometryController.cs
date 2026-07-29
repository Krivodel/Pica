using Avalonia;
using Avalonia.Controls;

using Pica.Viewer.Services;

namespace Pica.Viewer.Views;

internal sealed class ViewerWindowGeometryController
{
    internal bool IsApplying => _isApplying;

    private const double WindowedTitleBarHeight = 36d;

    private readonly ImageViewerWindow _window;
    private readonly ImageViewerView _view;
    private bool _isApplying;

    internal ViewerWindowGeometryController(
        ImageViewerWindow window,
        ImageViewerView view)
    {
        _window = window
            ?? throw new ArgumentNullException(nameof(window));
        _view = view ?? throw new ArgumentNullException(nameof(view));
    }

    internal void Apply(Action applyGeometry)
    {
        ArgumentNullException.ThrowIfNull(applyGeometry);
        _isApplying = true;

        try
        {
            applyGeometry();
        }
        finally
        {
            _isApplying = false;
        }
    }

    internal void ApplyRectangle(WindowRectangle rectangle)
    {
        double scaling = _window.RenderScaling;

        Apply(() =>
        {
            _window.Width = rectangle.Width / scaling;
            _window.Height = rectangle.Height / scaling;
            _window.Position = new PixelPoint(
                rectangle.Left,
                rectangle.Top);
        });
    }

    internal double GetWindowedTitleBarHeight()
    {
        double measuredHeight =
            _window.ClientSize.Height
            - _view.ViewerArea.Bounds.Height;

        return measuredHeight > 0d
            ? measuredHeight
            : WindowedTitleBarHeight;
    }
}
