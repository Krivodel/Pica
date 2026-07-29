using System.Reflection;

using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace Pica.Viewer.Tests.TestDoubles;

internal sealed class RecordingStorageProvider : IDisposable
{
    internal IStorageProvider Provider { get; }
    internal string? SuggestedFileName { get; private set; }
    internal bool? SavePickerHasUiThreadAccess { get; private set; }
    internal RecordingStorageFile Destination { get; } = new();
    internal IStorageFile? SaveDestination { get; private set; }

    internal RecordingStorageProvider()
    {
        IStorageProvider provider =
            DispatchProxy.Create<IStorageProvider, StorageProviderDispatchProxy>();
        StorageProviderDispatchProxy proxy =
            (StorageProviderDispatchProxy)(object)provider;
        proxy.Initialize(this);
        Provider = provider;
        SaveDestination = Destination.File;
    }

    public void Dispose()
    {
        Destination.Dispose();
    }

    internal void RecordSave(FilePickerSaveOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        SuggestedFileName = options.SuggestedFileName;
        SavePickerHasUiThreadAccess =
            Dispatcher.UIThread.CheckAccess();
    }

    internal void CancelSave()
    {
        SaveDestination = null;
    }
}
