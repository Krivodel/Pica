using Avalonia;
using Avalonia.Controls;
using Avalonia.Rendering;

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
                && (this.TranslatePoint(point, control) is Point controlPoint)
                && new Rect(control.Bounds.Size).Contains(controlPoint))
            {
                return false;
            }
        }

        return true;
    }
}
