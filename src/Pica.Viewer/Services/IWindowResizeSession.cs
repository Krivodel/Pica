using Avalonia;

namespace Pica.Viewer.Services;

internal interface IWindowResizeSession
{
    WindowRectangle Calculate(PixelPoint pointerPosition);
}
