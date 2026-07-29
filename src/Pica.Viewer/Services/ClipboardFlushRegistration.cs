namespace Pica.Viewer.Services;

internal sealed class ClipboardFlushRegistration : IDisposable
{
    private readonly ClipboardFlushCoordinator _coordinator;
    private AvaloniaClipboardDataWriter? _writer;

    internal ClipboardFlushRegistration(
        ClipboardFlushCoordinator coordinator,
        AvaloniaClipboardDataWriter writer)
    {
        _coordinator = coordinator
            ?? throw new ArgumentNullException(nameof(coordinator));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }

    public void Dispose()
    {
        AvaloniaClipboardDataWriter? writer =
            Interlocked.Exchange(ref _writer, null);

        if (writer is not null)
        {
            _coordinator.Unregister(writer);
        }
    }
}
