using Avalonia.Input;

using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.TestDoubles;

internal sealed class ConfiguredViewerClipboardShortcut : IViewerClipboardShortcut
{
    private readonly Key _key;
    private readonly KeyModifiers _modifiers;

    internal ConfiguredViewerClipboardShortcut(Key key, KeyModifiers modifiers = KeyModifiers.None)
    {
        _key = key;
        _modifiers = modifiers;
    }

    public bool Matches(Key key, PhysicalKey physicalKey, KeyModifiers modifiers)
    {
        return (key == _key) && (modifiers == _modifiers);
    }
}
