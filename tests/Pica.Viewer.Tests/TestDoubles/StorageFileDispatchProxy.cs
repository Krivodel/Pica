using System.Reflection;

namespace Pica.Viewer.Tests.TestDoubles;

internal class StorageFileDispatchProxy : DispatchProxy
{
    private RecordingStorageFile? _owner;

    internal void Initialize(RecordingStorageFile owner)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    protected override object? Invoke(
        MethodInfo? targetMethod,
        object?[]? args)
    {
        _ = args;
        MethodInfo method = targetMethod
            ?? throw new ArgumentNullException(nameof(targetMethod));
        RecordingStorageFile owner = _owner
            ?? throw new InvalidOperationException(
                "The storage file proxy has not been initialized.");

        return method.Name switch
        {
            "get_Name" => "recorded.png",
            "get_Path" => new Uri("file:///recorded.png"),
            "get_CanBookmark" => false,
            "OpenWriteAsync" => Task.FromResult(owner.ContentStream),
            "Dispose" => null,
            StorageDispatchProxyMemberNames.NonImplementableMemberName => null,
            _ => throw new NotSupportedException(
                $"The storage file member '{method.Name}' is not used by this test.")
        };
    }
}
