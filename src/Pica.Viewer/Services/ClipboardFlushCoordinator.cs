namespace Pica.Viewer.Services;

internal sealed class ClipboardFlushCoordinator : IClipboardImageWriter
{
    private readonly object _sync = new();
    private readonly HashSet<AvaloniaClipboardDataWriter> _writers = [];

    public async Task FlushAsync(CancellationToken ct)
    {
        IReadOnlyList<AvaloniaClipboardDataWriter> writers;

        lock (_sync)
        {
            writers = _writers.ToList();
        }

        foreach (AvaloniaClipboardDataWriter writer in writers)
        {
            await writer
                .FlushAsync(ct)
                .ConfigureAwait(false);
        }
    }

    internal IDisposable Register(AvaloniaClipboardDataWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        lock (_sync)
        {
            _writers.Add(writer);
        }

        return new ClipboardFlushRegistration(this, writer);
    }

    internal void Unregister(AvaloniaClipboardDataWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        lock (_sync)
        {
            _writers.Remove(writer);
        }
    }
}
