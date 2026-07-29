using System.Reflection;

using Avalonia.Input.Platform;

namespace Pica.Viewer.Tests.TestDoubles;

internal sealed class RecordingClipboard
{
    internal IClipboard Clipboard { get; }
    internal bool? SetDataHasUiThreadAccess { get; private set; }

    internal RecordingClipboard()
    {
        IClipboard clipboard =
            DispatchProxy.Create<IClipboard, ClipboardDispatchProxy>();
        ClipboardDispatchProxy proxy =
            (ClipboardDispatchProxy)(object)clipboard;
        proxy.Initialize(this);
        Clipboard = clipboard;
    }

    internal void RecordSetData(bool hasUiThreadAccess)
    {
        SetDataHasUiThreadAccess = hasUiThreadAccess;
    }
}
