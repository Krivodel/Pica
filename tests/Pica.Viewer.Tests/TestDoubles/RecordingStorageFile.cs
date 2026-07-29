using System.Reflection;

using Avalonia.Platform.Storage;

namespace Pica.Viewer.Tests.TestDoubles;

internal sealed class RecordingStorageFile : IDisposable
{
    internal IStorageFile File { get; }
    internal byte[] Content => _content.ToArray();
    internal Stream ContentStream => _content;

    private readonly MemoryStream _content = new();

    internal RecordingStorageFile()
    {
        IStorageFile file =
            DispatchProxy.Create<IStorageFile, StorageFileDispatchProxy>();
        StorageFileDispatchProxy proxy =
            (StorageFileDispatchProxy)(object)file;
        proxy.Initialize(this);
        File = file;
    }

    public void Dispose()
    {
        _content.Dispose();
    }
}
