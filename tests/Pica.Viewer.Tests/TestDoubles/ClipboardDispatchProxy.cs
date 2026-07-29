using System.Reflection;

using Avalonia.Threading;

namespace Pica.Viewer.Tests.TestDoubles;

internal class ClipboardDispatchProxy : DispatchProxy
{
    private RecordingClipboard? _owner;

    internal void Initialize(RecordingClipboard owner)
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
        RecordingClipboard owner = _owner
            ?? throw new InvalidOperationException(
                "The clipboard proxy has not been initialized.");

        if (string.Equals(
                method.Name,
                "SetDataAsync",
                StringComparison.Ordinal))
        {
            owner.RecordSetData(Dispatcher.UIThread.CheckAccess());

            return Task.CompletedTask;
        }

        throw new NotSupportedException(
            $"The clipboard member '{method.Name}' is not used by this test.");
    }
}
