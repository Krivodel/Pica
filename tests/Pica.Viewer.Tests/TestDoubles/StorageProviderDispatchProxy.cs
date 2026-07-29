using System.Reflection;

using Avalonia.Platform.Storage;

namespace Pica.Viewer.Tests.TestDoubles;

internal class StorageProviderDispatchProxy : DispatchProxy
{
    private RecordingStorageProvider? _owner;

    internal void Initialize(RecordingStorageProvider owner)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    protected override object? Invoke(
        MethodInfo? targetMethod,
        object?[]? args)
    {
        MethodInfo method = targetMethod
            ?? throw new ArgumentNullException(nameof(targetMethod));
        RecordingStorageProvider owner = _owner
            ?? throw new InvalidOperationException(
                "The storage provider proxy has not been initialized.");

        return method.Name switch
        {
            "get_CanOpen" => false,
            "get_CanSave" => true,
            "get_CanPickFolder" => false,
            "SaveFilePickerAsync" => CreateSaveResult(owner, args),
            StorageDispatchProxyMemberNames.NonImplementableMemberName => null,
            _ => throw new NotSupportedException(
                $"The storage provider member '{method.Name}' is not used by this test.")
        };
    }

    private static Task<IStorageFile?> CreateSaveResult(
        RecordingStorageProvider owner,
        object?[]? args)
    {
        if (args is not [FilePickerSaveOptions options])
        {
            throw new ArgumentException(
                "The save picker call must contain its options.",
                nameof(args));
        }

        owner.RecordSave(options);

        return Task.FromResult(owner.SaveDestination);
    }
}
