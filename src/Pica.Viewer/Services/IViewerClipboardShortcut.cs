using Avalonia.Input;

namespace Pica.Viewer.Services;

public interface IViewerClipboardShortcut
{
    bool Matches(Key key, PhysicalKey physicalKey, KeyModifiers modifiers);
}
