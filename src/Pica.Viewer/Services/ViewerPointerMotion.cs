using Avalonia;

namespace Pica.Viewer.Services;

internal static class ViewerPointerMotion
{
    public static bool HasMoved(PixelPoint? previousPosition, PixelPoint currentPosition)
    {
        return previousPosition is { } position
            && (position != currentPosition);
    }
}
