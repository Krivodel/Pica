using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Rendering;
using Avalonia.VisualTree;

namespace Pica.Viewer.Controls;

internal sealed class ViewerWindowResizeBorderControl : Border, ICustomHitTest
{
    internal IReadOnlyList<Control> InputExclusions { get; set; } = Array.Empty<Control>();

    private const double MinimumResizeEdgeThickness = 2d;

    public bool HitTest(Point point)
    {
        if (!new Rect(Bounds.Size).Contains(point))
        {
            return false;
        }

        if (IsOnWindowEdge(point))
        {
            return true;
        }

        foreach (Control control in InputExclusions)
        {
            if (control.IsEffectivelyVisible
                && control.IsHitTestVisible
                && control.GetVisualAncestors().OfType<InputElement>()
                    .All(ancestor => ancestor.IsHitTestVisible)
                && (this.TranslatePoint(point, control) is Point controlPoint)
                && new Rect(control.Bounds.Size).Contains(controlPoint))
            {
                return false;
            }
        }

        return true;
    }

    private bool IsOnWindowEdge(Point point)
    {
        TopLevel? topLevel = TopLevel.GetTopLevel(this);

        if ((topLevel is null)
            || (this.TranslatePoint(point, topLevel) is not Point windowPoint))
        {
            return false;
        }

        Size clientSize = topLevel.ClientSize;

        return (windowPoint.X < MinimumResizeEdgeThickness)
            || (windowPoint.Y < MinimumResizeEdgeThickness)
            || (windowPoint.X >= clientSize.Width - MinimumResizeEdgeThickness)
            || (windowPoint.Y >= clientSize.Height - MinimumResizeEdgeThickness);
    }
}
