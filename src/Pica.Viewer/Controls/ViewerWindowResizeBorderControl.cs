using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Rendering;
using Avalonia.VisualTree;

namespace Pica.Viewer.Controls;

internal sealed class ViewerWindowResizeBorderControl : Border, ICustomHitTest
{
    internal IReadOnlyList<Control> InputExclusions { get; set; } = Array.Empty<Control>();

    public bool HitTest(Point point)
    {
        if (!new Rect(Bounds.Size).Contains(point))
        {
            return false;
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
}
